using System.Text;
using Egle360.Pdf.Native;

namespace Egle360.Pdf;

/// <summary>
/// Fügt einer Seite Vektorinhalte hinzu. Koordinaten in Punkt im PDF-Seitenraum (Ursprung unten links).
/// Erst <see cref="Commit"/> schreibt die Inhalte in die Seite.
/// </summary>
public sealed unsafe class PdfContentWriter : IDisposable
{
    private readonly PdfDocument _document;
    private nint _page;

    internal PdfContentWriter(PdfDocument document, nint page)
    {
        _document = document;
        _page = page;
    }

    public PdfContentWriter Line(double x1, double y1, double x2, double y2, PdfColor color, double width = 1)
    {
        lock (PdfiumLibrary.Sync)
        {
            var path = Pdfium.FPDFPageObj_CreateNewPath((float)x1, (float)y1);
            Pdfium.FPDFPath_LineTo(path, (float)x2, (float)y2);
            Stroke(path, color, width);
            Pdfium.FPDFPath_SetDrawMode(path, Pdfium.FPDF_FILLMODE_NONE, 1);
            Pdfium.FPDFPage_InsertObject(Page, path);
        }
        return this;
    }

    /// <summary>Geschlossener oder offener Linienzug aus Punktpaaren.</summary>
    public PdfContentWriter Polyline(IReadOnlyList<(double X, double Y)> points, PdfColor color, double width = 1, bool closed = false)
    {
        if (points.Count < 2)
        {
            throw new ArgumentException("Ein Linienzug braucht mindestens zwei Punkte.", nameof(points));
        }
        lock (PdfiumLibrary.Sync)
        {
            var path = Pdfium.FPDFPageObj_CreateNewPath((float)points[0].X, (float)points[0].Y);
            for (var i = 1; i < points.Count; i++)
            {
                Pdfium.FPDFPath_LineTo(path, (float)points[i].X, (float)points[i].Y);
            }
            if (closed)
            {
                Pdfium.FPDFPath_Close(path);
            }
            Stroke(path, color, width);
            Pdfium.FPDFPath_SetDrawMode(path, Pdfium.FPDF_FILLMODE_NONE, 1);
            Pdfium.FPDFPage_InsertObject(Page, path);
        }
        return this;
    }

    public PdfContentWriter Rectangle(double x, double y, double width, double height, PdfColor? stroke, PdfColor? fill = null, double lineWidth = 1)
    {
        lock (PdfiumLibrary.Sync)
        {
            var rect = Pdfium.FPDFPageObj_CreateNewRect((float)x, (float)y, (float)width, (float)height);
            if (stroke is { } s)
            {
                Stroke(rect, s, lineWidth);
            }
            if (fill is { } f)
            {
                Pdfium.FPDFPageObj_SetFillColor(rect, f.R, f.G, f.B, f.A);
            }
            Pdfium.FPDFPath_SetDrawMode(rect, fill is null ? Pdfium.FPDF_FILLMODE_NONE : Pdfium.FPDF_FILLMODE_ALTERNATE, stroke is null ? 0 : 1);
            Pdfium.FPDFPage_InsertObject(Page, rect);
        }
        return this;
    }

    /// <summary>Text in einer der 14 Standardschriften (z. B. „Helvetica“), Grundlinie bei (x, y).</summary>
    public PdfContentWriter Text(double x, double y, string text, double size, PdfColor color, string font = "Helvetica")
    {
        lock (PdfiumLibrary.Sync)
        {
            var fontBytes = Encoding.ASCII.GetBytes(font + "\0");
            nint obj;
            fixed (byte* f = fontBytes)
            {
                obj = Pdfium.FPDFPageObj_NewTextObj(_document.Handle, f, (float)size);
            }
            if (obj == 0)
            {
                throw new PdfException(PdfErrorKind.Unknown, $"Die Schrift {font} ist nicht verfügbar.");
            }
            fixed (char* t = text + "\0")
            {
                Pdfium.FPDFText_SetText(obj, t);
            }
            Pdfium.FPDFPageObj_SetFillColor(obj, color.R, color.G, color.B, color.A);
            Pdfium.FPDFPageObj_Transform(obj, 1, 0, 0, 1, x, y);
            Pdfium.FPDFPage_InsertObject(Page, obj);
        }
        return this;
    }

    /// <summary>Schreibt die hinzugefügten Objekte in den Inhaltsstrom der Seite.</summary>
    public void Commit()
    {
        lock (PdfiumLibrary.Sync)
        {
            if (Pdfium.FPDFPage_GenerateContent(Page) == 0)
            {
                throw new PdfException(PdfErrorKind.Page, "Der Seiteninhalt konnte nicht geschrieben werden.");
            }
            _document.MarkContentChanged();
        }
    }

    public void Dispose()
    {
        lock (PdfiumLibrary.Sync)
        {
            if (_page != 0)
            {
                Pdfium.FPDF_ClosePage(_page);
                _page = 0;
            }
        }
    }

    private nint Page => _page != 0 ? _page : throw new ObjectDisposedException(nameof(PdfContentWriter));

    private static void Stroke(nint obj, PdfColor color, double width)
    {
        Pdfium.FPDFPageObj_SetStrokeColor(obj, color.R, color.G, color.B, color.A);
        Pdfium.FPDFPageObj_SetStrokeWidth(obj, (float)width);
    }
}
