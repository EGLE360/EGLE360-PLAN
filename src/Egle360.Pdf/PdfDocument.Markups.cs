using System.Runtime.InteropServices;
using System.Text;
using Egle360.Pdf.Markups;
using Egle360.Pdf.Native;
using Egle360.Pdf.Viewing;

namespace Egle360.Pdf;

/// <summary>Markierungen als PDF-Anmerkungen lesen und schreiben.</summary>
public sealed unsafe partial class PdfDocument
{
    /// <summary>Markierungen je Seite, wie sie zuletzt gelesen oder geschrieben wurden.</summary>
    private readonly Dictionary<int, IReadOnlyList<Markup>> _markups = new();

    /// <summary>Anzahl der seit dem letzten Laden angelegten Anmerkungen mit Hilfsschlüssel für Standardeinträge.</summary>
    private int _pendingStandardPatches;

    /// <summary>Zählt Änderungen der Seitenstruktur (Einfügen, Löschen, Verschieben). Seitenbezogene Zustände werden dann ungültig.</summary>
    public int StructureVersion { get; private set; }

    /// <summary>Wird nach dem Schreiben der Markierungen einer Seite ausgelöst (Argument: Seitenindex).</summary>
    public event EventHandler<int>? MarkupsChanged;

    /// <summary>Rahmen und Drehung einer Seite für die Umrechnung von Koordinaten.</summary>
    public PageTransform GetPageTransform(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            var page = GetPage(pageIndex);
            Pdfium.FS_RECTF box;
            if (Pdfium.FPDF_GetPageBoundingBox(page, &box) == 0)
            {
                throw PdfiumLibrary.LastError($"Seite {pageIndex + 1}");
            }
            var rect = new PdfRect(Math.Min(box.left, box.right), Math.Min(box.bottom, box.top), Math.Max(box.left, box.right), Math.Max(box.bottom, box.top));
            return new PageTransform(rect, Pdfium.FPDFPage_GetRotation(page) * 90);
        }
    }

    /// <summary>
    /// Von EGLE360 angelegte Markierungen der Seite in Zeichenreihenfolge. Anmerkungen anderer Programme und
    /// solche, deren Rahmen nicht mehr zu den EGLE360-Daten passt (in einem anderen Programm verändert), gehören
    /// nicht dazu; sie werden von PDFium dargestellt.
    /// </summary>
    public IReadOnlyList<Markup> GetMarkups(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            if (_markups.TryGetValue(pageIndex, out var cached))
            {
                return cached;
            }
            var page = GetPage(pageIndex);
            var result = new List<Markup>();
            var count = Pdfium.FPDFPage_GetAnnotCount(page);
            for (var i = 0; i < count; i++)
            {
                var annot = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annot == 0)
                {
                    continue;
                }
                try
                {
                    if (ReadMarkup(annot) is { } m && !result.Any(r => r.Id == m.Id))
                    {
                        result.Add(m);
                    }
                }
                finally
                {
                    Pdfium.FPDFPage_CloseAnnot(annot);
                }
            }
            _markups[pageIndex] = result;
            return result;
        }
    }

    /// <summary>
    /// Ersetzt alle EGLE360-Markierungen der Seite. Die Anmerkungen werden neu angelegt, damit Reihenfolge und
    /// Inhalt genau dem Modell entsprechen; Anmerkungen anderer Programme bleiben unberührt.
    /// </summary>
    public void SetMarkups(int pageIndex, IReadOnlyList<Markup> markups, ITextOutliner outliner)
    {
        // Text in Umrisse umsetzen, bevor die Sperre genommen wird (die Oberfläche braucht dafür ihren eigenen Thread).
        var shapes = markups.Select(m => (Markup: m, Shape: MarkupGeometry.BuildShape(m, outliner))).ToList();
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanAnnotate();
            CheckIndex(pageIndex);
            var existing = GetMarkups(pageIndex).Select(m => m.Id).ToHashSet();
            var page = GetPage(pageIndex);
            for (var i = Pdfium.FPDFPage_GetAnnotCount(page) - 1; i >= 0; i--)
            {
                var annot = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annot == 0)
                {
                    continue;
                }
                var remove = ReadString(annot, "NM") is { } id && existing.Contains(id) && ReadString(annot, MarkupSerializer.DataKey) is not null;
                Pdfium.FPDFPage_CloseAnnot(annot);
                if (remove)
                {
                    Pdfium.FPDFPage_RemoveAnnot(page, i);
                }
            }
            foreach (var (markup, shape) in shapes)
            {
                WriteMarkup(page, markup, shape);
            }
            _markups[pageIndex] = markups.ToArray();
            IsModified = true;
        }
        MarkupsChanged?.Invoke(this, pageIndex);
    }

    /// <summary>Anmerkungen dürfen bei Berechtigung „Anmerkungen“ oder „Ändern“ bearbeitet werden.</summary>
    public bool CanAnnotate => Permissions.HasFlag(PdfPermissions.Annotate) || Permissions.HasFlag(PdfPermissions.Modify);

    private void EnsureCanAnnotate()
    {
        if (!CanAnnotate)
        {
            throw new PdfException(PdfErrorKind.Permission, PdfException.Describe(PdfErrorKind.Permission));
        }
    }

    /// <summary>Blendet beim Rendern die EGLE360-Markierungen der Seite aus (die Oberfläche zeichnet sie selbst). Aufrufer hält die Sperre.</summary>
    private List<(nint Annot, int Flags)> HideMarkups(int pageIndex, nint page)
    {
        var hidden = new List<(nint, int)>();
        var ids = GetMarkups(pageIndex).Select(m => m.Id).ToHashSet();
        if (ids.Count == 0)
        {
            return hidden;
        }
        var count = Pdfium.FPDFPage_GetAnnotCount(page);
        for (var i = 0; i < count; i++)
        {
            var annot = Pdfium.FPDFPage_GetAnnot(page, i);
            if (annot == 0)
            {
                continue;
            }
            if (ReadString(annot, "NM") is { } id && ids.Contains(id) && ReadString(annot, MarkupSerializer.DataKey) is not null)
            {
                var flags = Pdfium.FPDFAnnot_GetFlags(annot);
                Pdfium.FPDFAnnot_SetFlags(annot, flags | Pdfium.FPDF_ANNOT_FLAG_HIDDEN);
                hidden.Add((annot, flags));
            }
            else
            {
                Pdfium.FPDFPage_CloseAnnot(annot);
            }
        }
        return hidden;
    }

    private static void RestoreMarkups(List<(nint Annot, int Flags)> hidden)
    {
        foreach (var (annot, flags) in hidden)
        {
            Pdfium.FPDFAnnot_SetFlags(annot, flags);
            Pdfium.FPDFPage_CloseAnnot(annot);
        }
    }

    private Markup? ReadMarkup(nint annot)
    {
        if (ReadString(annot, MarkupSerializer.DataKey) is not { } json || ReadString(annot, "NM") is not { } id)
        {
            return null;
        }
        var markup = MarkupSerializer.FromJson(json, id, ReadString(annot, "Contents") ?? "", ReadString(annot, "T") ?? "", ParsePdfDate(ReadString(annot, "M")));
        if (markup is null)
        {
            return null;
        }
        Pdfium.FS_RECTF r;
        if (Pdfium.FPDFAnnot_GetRect(annot, &r) == 0)
        {
            return null;
        }
        var expected = MarkupGeometry.Bounds(markup);
        const double tolerance = 0.5;
        var matches = Math.Abs(Math.Min(r.left, r.right) - expected.Left) < tolerance
            && Math.Abs(Math.Max(r.left, r.right) - expected.Right) < tolerance
            && Math.Abs(Math.Min(r.top, r.bottom) - expected.Bottom) < tolerance
            && Math.Abs(Math.Max(r.top, r.bottom) - expected.Top) < tolerance;
        return matches ? markup : null;
    }

    private void WriteMarkup(nint page, Markup m, MarkupShape shape)
    {
        var subtype = m.Kind switch
        {
            MarkupKind.Rectangle => Pdfium.FPDF_ANNOT_SQUARE,
            MarkupKind.Ellipse => Pdfium.FPDF_ANNOT_CIRCLE,
            MarkupKind.TextBox or MarkupKind.Callout or MarkupKind.Typewriter => Pdfium.FPDF_ANNOT_FREETEXT,
            MarkupKind.Note => Pdfium.FPDF_ANNOT_TEXT,
            MarkupKind.Highlight => Pdfium.FPDF_ANNOT_HIGHLIGHT,
            MarkupKind.Stamp or MarkupKind.Image => Pdfium.FPDF_ANNOT_STAMP,
            MarkupKind.Underline => Pdfium.FPDF_ANNOT_UNDERLINE,
            MarkupKind.StrikeOut => Pdfium.FPDF_ANNOT_STRIKEOUT,
            _ => Pdfium.FPDF_ANNOT_INK,
        };
        var annot = Pdfium.FPDFPage_CreateAnnot(page, subtype);
        if (annot == 0)
        {
            throw new PdfException(PdfErrorKind.Unknown, $"Die Markierung „{MarkupKinds.DisplayName(m.Kind)}“ konnte nicht angelegt werden.");
        }
        try
        {
            var bounds = shape.Bounds();
            var rect = new Pdfium.FS_RECTF { left = (float)bounds.Left, top = (float)bounds.Top, right = (float)bounds.Right, bottom = (float)bounds.Bottom };
            Pdfium.FPDFAnnot_SetRect(annot, &rect);

            // Farben vor dem Erscheinungsstrom setzen; PDFium lehnt das danach ab. Die Deckkraft landet in /CA.
            var alpha = (uint)Math.Round(Math.Clamp(m.Style.Opacity, 0, 1) * 255);
            var color = m.Kind == MarkupKind.Highlight ? m.Style.Fill ?? m.Style.Stroke : m.Style.Stroke;
            Pdfium.FPDFAnnot_SetColor(annot, Pdfium.FPDFANNOT_COLORTYPE_Color, color.R, color.G, color.B, alpha);
            if (MarkupKinds.CanFill(m.Kind) && m.Style.Fill is { } fill)
            {
                Pdfium.FPDFAnnot_SetColor(annot, Pdfium.FPDFANNOT_COLORTYPE_InteriorColor, fill.R, fill.G, fill.B, alpha);
            }
            if (m.Kind != MarkupKind.Highlight)
            {
                Pdfium.FPDFAnnot_SetBorder(annot, 0, 0, (float)m.Style.LineWidth);
            }
            Pdfium.FPDFAnnot_SetFlags(annot, Pdfium.FPDF_ANNOT_FLAG_PRINT | (m.Locked ? Pdfium.FPDF_ANNOT_FLAG_LOCKED : 0));

            switch (m.Kind)
            {
                case MarkupKind.Freehand:
                    foreach (var path in m.Paths.Where(p => p.Count > 0))
                    {
                        AddInk(annot, path);
                    }
                    break;
                case MarkupKind.Line or MarkupKind.Arrow or MarkupKind.Polyline or MarkupKind.Length or MarkupKind.Polylength or MarkupKind.Angle or MarkupKind.Dimension or MarkupKind.Radius or MarkupKind.Diameter:
                    AddInk(annot, m.Points);
                    break;
                case MarkupKind.Polygon or MarkupKind.Cloud or MarkupKind.Perimeter or MarkupKind.Area:
                    AddInk(annot, [.. m.Points, m.Points[0]]);
                    break;
                case MarkupKind.Count:
                    foreach (var p in m.Points)
                    {
                        AddInk(annot, [p, p + new PdfPoint(0.01, 0)]);
                    }
                    break;
                case MarkupKind.Highlight or MarkupKind.Underline or MarkupKind.StrikeOut:
                    foreach (var r in MarkupGeometry.TextRects(m))
                    {
                        var quad = new Pdfium.FS_QUADPOINTSF
                        {
                            x1 = (float)r.Left, y1 = (float)r.Top, x2 = (float)r.Right, y2 = (float)r.Top,
                            x3 = (float)r.Left, y3 = (float)r.Bottom, x4 = (float)r.Right, y4 = (float)r.Bottom,
                        };
                        Pdfium.FPDFAnnot_AppendAttachmentPoints(annot, &quad);
                    }
                    break;
                case MarkupKind.TextBox or MarkupKind.Callout or MarkupKind.Typewriter:
                    WriteString(annot, "DA", MarkupSerializer.DefaultAppearance(m));
                    break;
            }

            WriteString(annot, "NM", m.Id);
            WriteString(annot, "T", m.Author);
            WriteString(annot, "Subj", MarkupKinds.DisplayName(m.Kind));
            WriteString(annot, "Contents", m.Text);
            WriteString(annot, "M", MarkupSerializer.PdfDate(m.Modified));
            WriteString(annot, "CreationDate", MarkupSerializer.PdfDate(m.Created));
            WriteString(annot, MarkupSerializer.DataKey, MarkupSerializer.ToJson(m));
            // In verschlüsselten Dokumenten sind Zeichenketten in der Datei verschlüsselt; der Hilfsschlüssel ließe
            // sich dort nicht auswerten. Die Anmerkung bleibt dann eine gültige Ink-Anmerkung.
            if (MarkupSerializer.StandardEntries(m) is { } std && !IsEncryptedNoLock)
            {
                WriteString(annot, MarkupSerializer.StandardKey, std);
                _pendingStandardPatches++;
            }

            fixed (char* ap = MarkupSerializer.AppearanceStream(shape) + "\0")
            {
                if (Pdfium.FPDFAnnot_SetAP(annot, Pdfium.FPDF_ANNOT_APPEARANCEMODE_NORMAL, ap) == 0)
                {
                    throw new PdfException(PdfErrorKind.Unknown, "Die Darstellung der Markierung konnte nicht geschrieben werden.");
                }
            }
            if (shape.Image is { } image)
            {
                AppendImage(annot, image);
            }
        }
        finally
        {
            Pdfium.FPDFPage_CloseAnnot(annot);
        }
    }

    /// <summary>
    /// Hängt das Bild als Bildobjekt an die Darstellung der Anmerkung an. PDFium legt dabei ein Bild-XObject an und
    /// für die Transparenz eine Soft-Maske.
    /// </summary>
    private void AppendImage(nint annot, ShapeImage image)
    {
        var img = image.Image;
        var obj = Pdfium.FPDFPageObj_NewImageObj(Handle);
        if (obj == 0)
        {
            throw new PdfException(PdfErrorKind.Unknown, "Das Bild konnte nicht angelegt werden.");
        }
        var appended = false;
        try
        {
            fixed (byte* pixels = img.Bgra)
            {
                var bitmap = Pdfium.FPDFBitmap_CreateEx(img.Width, img.Height, Pdfium.FPDFBitmap_BGRA, pixels, img.Width * 4);
                if (bitmap == 0)
                {
                    throw new PdfException(PdfErrorKind.Unknown, "Das Bild konnte nicht angelegt werden.");
                }
                try
                {
                    if (Pdfium.FPDFImageObj_SetBitmap(null, 0, obj, bitmap) == 0)
                    {
                        throw new PdfException(PdfErrorKind.Unknown, "Das Bild konnte nicht übernommen werden.");
                    }
                }
                finally
                {
                    Pdfium.FPDFBitmap_Destroy(bitmap);
                }
            }
            Pdfium.FPDFPageObj_Transform(obj, image.A, image.B, image.C, image.D, image.E, image.F);
            if (Pdfium.FPDFAnnot_AppendObject(annot, obj) == 0)
            {
                throw new PdfException(PdfErrorKind.Unknown, "Das Bild konnte nicht in die Markierung übernommen werden.");
            }
            appended = true;
        }
        finally
        {
            // Nach dem Anhängen gehört das Objekt der Anmerkung.
            if (!appended)
            {
                Pdfium.FPDFPageObj_Destroy(obj);
            }
        }
    }

    private bool IsEncryptedNoLock => Pdfium.FPDF_GetSecurityHandlerRevision(Handle) != -1;

    private static void AddInk(nint annot, IReadOnlyList<PdfPoint> points)
    {
        var buffer = new Pdfium.FS_POINTF[points.Count];
        for (var i = 0; i < points.Count; i++)
        {
            buffer[i] = new Pdfium.FS_POINTF { x = (float)points[i].X, y = (float)points[i].Y };
        }
        fixed (Pdfium.FS_POINTF* p = buffer)
        {
            Pdfium.FPDFAnnot_AddInkStroke(annot, p, (nuint)buffer.Length);
        }
    }

    private static void WriteString(nint annot, string key, string value)
    {
        var k = Encoding.ASCII.GetBytes(key + "\0");
        fixed (byte* kp = k)
        fixed (char* v = value + "\0")
        {
            Pdfium.FPDFAnnot_SetStringValue(annot, kp, v);
        }
    }

    internal static string? ReadString(nint annot, string key)
    {
        var k = Encoding.ASCII.GetBytes(key + "\0");
        fixed (byte* kp = k)
        {
            if (Pdfium.FPDFAnnot_GetValueType(annot, kp) != Pdfium.FPDF_OBJECT_STRING)
            {
                return null;
            }
            var length = (int)Pdfium.FPDFAnnot_GetStringValue(annot, kp, null, new CULong(0)).Value;
            if (length < 2)
            {
                return null;
            }
            var buffer = new byte[length];
            fixed (byte* b = buffer)
            {
                Pdfium.FPDFAnnot_GetStringValue(annot, kp, b, new CULong((uint)length));
            }
            return Encoding.Unicode.GetString(buffer, 0, length - 2);
        }
    }

    /// <summary>Nach Strukturänderungen passen Seitenindizes nicht mehr. Aufrufer hält die Sperre.</summary>
    private void ResetMarkupState()
    {
        _markups.Clear();
        _scales.Clear();
        StructureVersion++;
    }

    // ---------- Seitenmaßstab ----------

    /// <summary>Privater Schlüssel der verborgenen Anmerkung, die den Maßstab einer Seite trägt.</summary>
    public const string ScaleKey = "E360Scale";

    private readonly Dictionary<int, MeasureScale?> _scales = new();

    /// <summary>Wird ausgelöst, wenn sich der Maßstab einer Seite ändert (Argument: Seitenindex).</summary>
    public event EventHandler<int>? PageScaleChanged;

    /// <summary>Maßstab der Seite für Messungen; null, solange keiner festgelegt ist.</summary>
    public MeasureScale? GetPageScale(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            if (_scales.TryGetValue(pageIndex, out var cached))
            {
                return cached;
            }
            MeasureScale? scale = null;
            var page = GetPage(pageIndex);
            var count = Pdfium.FPDFPage_GetAnnotCount(page);
            for (var i = 0; i < count && scale is null; i++)
            {
                var annot = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annot == 0)
                {
                    continue;
                }
                if (ReadString(annot, ScaleKey) is { } json)
                {
                    scale = MarkupSerializer.ScaleFromJson(json);
                }
                Pdfium.FPDFPage_CloseAnnot(annot);
            }
            _scales[pageIndex] = scale;
            return scale;
        }
    }

    /// <summary>
    /// Legt den Maßstab der Seite fest. Er wird als verborgene Anmerkung in der Seite gespeichert, damit er nach dem
    /// Speichern erhalten bleibt; andere Programme zeigen sie nicht an.
    /// </summary>
    public void SetPageScale(int pageIndex, MeasureScale? scale)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanAnnotate();
            CheckIndex(pageIndex);
            var page = GetPage(pageIndex);
            for (var i = Pdfium.FPDFPage_GetAnnotCount(page) - 1; i >= 0; i--)
            {
                var annot = Pdfium.FPDFPage_GetAnnot(page, i);
                if (annot == 0)
                {
                    continue;
                }
                var isScale = ReadString(annot, ScaleKey) is not null;
                Pdfium.FPDFPage_CloseAnnot(annot);
                if (isScale)
                {
                    Pdfium.FPDFPage_RemoveAnnot(page, i);
                }
            }
            if (scale is not null)
            {
                var annot = Pdfium.FPDFPage_CreateAnnot(page, Pdfium.FPDF_ANNOT_SQUARE);
                if (annot == 0)
                {
                    throw new PdfException(PdfErrorKind.Unknown, "Der Maßstab konnte nicht gespeichert werden.");
                }
                try
                {
                    var rect = new Pdfium.FS_RECTF { left = 0, bottom = 0, right = 1, top = 1 };
                    Pdfium.FPDFAnnot_SetRect(annot, &rect);
                    Pdfium.FPDFAnnot_SetFlags(annot, Pdfium.FPDF_ANNOT_FLAG_HIDDEN);
                    WriteString(annot, "NM", "e360-scale");
                    WriteString(annot, "Subj", "EGLE360 Maßstab");
                    WriteString(annot, ScaleKey, MarkupSerializer.ScaleToJson(scale));
                }
                finally
                {
                    Pdfium.FPDFPage_CloseAnnot(annot);
                }
            }
            _scales[pageIndex] = scale;
            IsModified = true;
        }
        PageScaleChanged?.Invoke(this, pageIndex);
    }
}
