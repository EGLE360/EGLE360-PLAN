using Egle360.Pdf.Markups;
using System.Runtime.InteropServices;
using Egle360.Pdf.Native;

namespace Egle360.Pdf;

/// <summary>Fangpunkte aus dem Planinhalt: End- und Mittelpunkte der Linien in Vektorplänen.</summary>
public sealed unsafe partial class PdfDocument
{
    /// <summary>Obergrenze je Seite, damit sehr dichte CAD-Pläne die Bedienung nicht ausbremsen.</summary>
    public const int MaxSnapPoints = 400_000;

    private const double MinSnapSegment = 2; // kürzere Strecken liefern keinen Mittelpunkt

    /// <summary>
    /// End- und Mittelpunkte der Pfade einer Seite in Benutzerkoordinaten (Linien, Rechtecke, Kurvenenden), auch aus
    /// eingebetteten Formularobjekten. Text und Bilder liefern keine Punkte, gescannte Pläne also keine.
    /// </summary>
    public SnapIndex GetSnapIndex(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            var points = new List<PdfPoint>();
            var page = GetPage(pageIndex);
            var count = Pdfium.FPDFPage_CountObjects(page);
            for (var i = 0; i < count && points.Count < MaxSnapPoints; i++)
            {
                CollectSnapPoints(Pdfium.FPDFPage_GetObject(page, i), Identity, points, 0);
            }
            return new SnapIndex(points);
        }
    }

    private static readonly Pdfium.FS_MATRIX Identity = new() { a = 1, d = 1 };

    private static void CollectSnapPoints(nint obj, Pdfium.FS_MATRIX parent, List<PdfPoint> points, int depth)
    {
        if (obj == 0 || depth > 8)
        {
            return;
        }
        Pdfium.FS_MATRIX own;
        if (Pdfium.FPDFPageObj_GetMatrix(obj, &own) == 0)
        {
            own = Identity;
        }
        var m = Multiply(own, parent);
        switch (Pdfium.FPDFPageObj_GetType(obj))
        {
            case Pdfium.FPDF_PAGEOBJ_PATH:
                CollectPath(obj, m, points);
                break;
            case Pdfium.FPDF_PAGEOBJ_FORM:
                var n = Pdfium.FPDFFormObj_CountObjects(obj);
                for (var i = 0; i < n && points.Count < MaxSnapPoints; i++)
                {
                    CollectSnapPoints(Pdfium.FPDFFormObj_GetObject(obj, new CULong((uint)i)), m, points, depth + 1);
                }
                break;
        }
    }

    private static void CollectPath(nint path, Pdfium.FS_MATRIX m, List<PdfPoint> points)
    {
        var count = Pdfium.FPDFPath_CountSegments(path);
        PdfPoint? last = null;
        PdfPoint? start = null;
        var bezier = 0;
        for (var i = 0; i < count && points.Count < MaxSnapPoints; i++)
        {
            var seg = Pdfium.FPDFPath_GetPathSegment(path, i);
            float x, y;
            if (seg == 0 || Pdfium.FPDFPathSegment_GetPoint(seg, &x, &y) == 0)
            {
                continue;
            }
            var p = Apply(m, x, y);
            switch (Pdfium.FPDFPathSegment_GetType(seg))
            {
                case Pdfium.FPDF_SEGMENT_MOVETO:
                    start = p;
                    points.Add(p);
                    bezier = 0;
                    break;
                case Pdfium.FPDF_SEGMENT_LINETO:
                    AddSegment(last, p, points);
                    points.Add(p);
                    bezier = 0;
                    break;
                case Pdfium.FPDF_SEGMENT_BEZIERTO:
                    // Je Kurve drei Punkte: zwei Kontrollpunkte und der Endpunkt; nur der Endpunkt zählt.
                    bezier = (bezier + 1) % 3;
                    if (bezier != 0)
                    {
                        continue;
                    }
                    points.Add(p);
                    break;
                default:
                    continue;
            }
            last = p;
            if (Pdfium.FPDFPathSegment_GetClose(seg) != 0 && start is { } s0)
            {
                AddSegment(p, s0, points);
                last = s0;
            }
        }
    }

    private static void AddSegment(PdfPoint? from, PdfPoint to, List<PdfPoint> points)
    {
        if (from is { } a && a.DistanceTo(to) >= MinSnapSegment)
        {
            points.Add(new PdfPoint((a.X + to.X) / 2, (a.Y + to.Y) / 2));
        }
    }

    /// <summary>Erst <paramref name="first"/>, dann <paramref name="second"/> (PDF-Matrizen, Zeilenvektor-Konvention).</summary>
    private static Pdfium.FS_MATRIX Multiply(Pdfium.FS_MATRIX first, Pdfium.FS_MATRIX second) => new()
    {
        a = first.a * second.a + first.b * second.c,
        b = first.a * second.b + first.b * second.d,
        c = first.c * second.a + first.d * second.c,
        d = first.c * second.b + first.d * second.d,
        e = first.e * second.a + first.f * second.c + second.e,
        f = first.e * second.b + first.f * second.d + second.f,
    };

    private static PdfPoint Apply(Pdfium.FS_MATRIX m, double x, double y) =>
        new(m.a * x + m.c * y + m.e, m.b * x + m.d * y + m.f);
}
