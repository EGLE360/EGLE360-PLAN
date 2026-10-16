using System.Runtime.InteropServices;
using System.Text;
using Egle360.Pdf.Markups;
using Egle360.Pdf.Native;

namespace Egle360.Pdf.Tests;

/// <summary>Markierungen als PDF-Anmerkungen: Schreiben, Speichern, Wiedereinlesen, Darstellung und Fremdprogramme.</summary>
public unsafe class MarkupPdfTests
{
    private static readonly ITextOutliner Text = new BlockTextOutliner();

    private static Markup Make(MarkupKind kind, params PdfPoint[] pts) =>
        Markup.Create(kind, [pts], MarkupStyle.DefaultFor(kind), "Prüfer");

    /// <summary>Je eine Markierung jeder Art, auf einer A4-Seite verteilt.</summary>
    private static List<Markup> AllKinds() =>
    [
        Make(MarkupKind.Rectangle, new(50, 700), new(150, 780)),
        Make(MarkupKind.Ellipse, new(200, 700), new(300, 780)) with { Style = MarkupStyle.DefaultFor(MarkupKind.Ellipse) with { Fill = new PdfColor(200, 230, 255) } },
        Make(MarkupKind.Line, new(50, 600), new(150, 650)),
        Make(MarkupKind.Arrow, new(200, 600), new(300, 650)),
        Make(MarkupKind.Polyline, new(350, 600), new(400, 650), new(450, 600)),
        Make(MarkupKind.Polygon, new(50, 450), new(150, 450), new(100, 550)),
        Make(MarkupKind.Cloud, new(200, 450), new(300, 450), new(300, 550), new(200, 550)),
        Markup.Create(MarkupKind.Freehand, [new PdfPoint[] { new(350, 450), new(380, 500), new(410, 460) }, new PdfPoint[] { new(420, 450), new(450, 520) }], MarkupStyle.DefaultFor(MarkupKind.Freehand), "Prüfer"),
        Make(MarkupKind.TextBox, new(50, 300), new(250, 360)).WithText("Tür prüfen (Brandschutz)"),
        Make(MarkupKind.Note, new PdfPoint(300, 360)).WithText("Notiz mit Umlauten: äöü ß"),
        Make(MarkupKind.Highlight, new(350, 300), new(500, 340)),
    ];

    [Fact]
    public void AllKindsSurviveSaveAndReopen()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("reference.pdf", dir);
        var original = File.ReadAllBytes(path);
        var markups = AllKinds();
        const int page = 2; // A4

        using (var doc = PdfDocument.Open(path))
        {
            Assert.Empty(doc.GetMarkups(page));
            doc.SetMarkups(page, markups, Text);
            Assert.True(doc.IsModified);
            Assert.Equal(PdfSaveMode.Incremental, doc.DefaultSaveMode);
            doc.Save();
            Assert.Equal(markups.Count, doc.GetMarkups(page).Count);
        }

        var saved = File.ReadAllBytes(path);
        Assert.Equal(original, saved[..original.Length]);
        var tail = Encoding.Latin1.GetString(saved, original.Length, saved.Length - original.Length);
        Assert.DoesNotContain("/" + MarkupSerializer.StandardKey, tail);

        using var reopened = PdfDocument.Open(path);
        var read = reopened.GetMarkups(page);
        Assert.Equal(markups.Select(m => m.Id), read.Select(m => m.Id));
        foreach (var (a, b) in markups.Zip(read))
        {
            Assert.Equal(a.Kind, b.Kind);
            Assert.Equal(a.Paths.Count, b.Paths.Count);
            foreach (var (pa, pb) in a.Paths.Zip(b.Paths))
            {
                Assert.Equal(pa, pb);
            }
            Assert.Equal(a.Style, b.Style);
            Assert.Equal(a.Text, b.Text);
            Assert.Equal("Prüfer", b.Author);
        }
    }

    [Fact]
    public void StandardKeysAreWrittenForOtherPrograms()
    {
        using var dir = new TempDir();
        var path = dir.File("standard.pdf");
        var line = Make(MarkupKind.Arrow, new(10, 20), new(110, 70));
        var polygon = Make(MarkupKind.Cloud, new(200, 200), new(300, 200), new(250, 300));
        var polyline = Make(MarkupKind.Polyline, new(10, 400), new(60, 450), new(110, 400));
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 595, 842);
            doc.SetMarkups(0, [line, polygon, polyline], Text);
            doc.SaveAs(path);
        }

        var text = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert.Contains("/LE [/None /OpenArrow]", text);
        Assert.Contains("/BE <</S /C /I 1>>", text);
        Assert.DoesNotContain("/InkList", text);
        Assert.DoesNotContain("/" + MarkupSerializer.StandardKey, text);

        using var reopened = PdfDocument.Open(path);
        var subtypes = new List<int>();
        lock (PdfiumLibrary.Sync)
        {
            var page = Pdfium.FPDF_LoadPage(reopened.Handle, 0);
            for (var i = 0; i < Pdfium.FPDFPage_GetAnnotCount(page); i++)
            {
                var annot = Pdfium.FPDFPage_GetAnnot(page, i);
                var subtype = Pdfium.FPDFAnnot_GetSubtype(annot);
                subtypes.Add(subtype);
                if (subtype == 4)
                {
                    Pdfium.FS_POINTF a, b;
                    Assert.NotEqual(0, Pdfium.FPDFAnnot_GetLine(annot, &a, &b));
                    Assert.Equal((10f, 20f, 110f, 70f), (a.x, a.y, b.x, b.y));
                }
                if (subtype == 7)
                {
                    var buffer = stackalloc Pdfium.FS_POINTF[8];
                    Assert.Equal(3u, (uint)Pdfium.FPDFAnnot_GetVertices(annot, buffer, new CULong(8)).Value);
                    Assert.Equal((250f, 300f), (buffer[2].x, buffer[2].y));
                }
                Pdfium.FPDFPage_CloseAnnot(annot);
            }
            Pdfium.FPDF_ClosePage(page);
        }
        Assert.Equal([4, 7, 8], subtypes); // Line, Polygon, PolyLine
        Assert.Equal(3, reopened.GetMarkups(0).Count);
    }

    [Fact]
    public void MarkupsAreRenderedAndCanBeHidden()
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 200, 200);
        var rect = Make(MarkupKind.Rectangle, new(50, 50), new(150, 150)) with
        {
            Style = new MarkupStyle(new PdfColor(0, 0, 0), new PdfColor(0, 0, 0), 1, 2),
        };
        doc.SetMarkups(0, [rect], Text);

        var buffer = new byte[200 * 200 * 4];
        doc.Render(0, 1, new PixelRect(0, 0, 200, 200), buffer, 800);
        Assert.True(Fixtures.Pixel(buffer, 800, 100, 100).IsBlack());
        doc.Render(0, 1, new PixelRect(0, 0, 200, 200), buffer, 800, hideMarkups: true);
        Assert.True(Fixtures.Pixel(buffer, 800, 100, 100).IsWhite());
        // Das Ausblenden ist vorübergehend.
        doc.Render(0, 1, new PixelRect(0, 0, 200, 200), buffer, 800);
        Assert.True(Fixtures.Pixel(buffer, 800, 100, 100).IsBlack());
    }

    [Fact]
    public void TransparencyIsStoredAndRendered()
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 100, 100);
        doc.SetMarkups(0, [Make(MarkupKind.Highlight, new(0, 0), new(100, 100)) with { Style = new MarkupStyle(PdfColor.Black, PdfColor.Black, 0.5, 0) }], Text);
        var buffer = new byte[100 * 100 * 4];
        doc.Render(0, 1, new PixelRect(0, 0, 100, 100), buffer, 400);
        var p = Fixtures.Pixel(buffer, 400, 50, 50);
        Assert.InRange(p.R, 110, 145);
    }

    [Fact]
    public void ChangedMarkupsReplaceTheOldAnnotations()
    {
        using var dir = new TempDir();
        var path = dir.File("edit.pdf");
        var a = Make(MarkupKind.Rectangle, new(10, 10), new(50, 50));
        var b = Make(MarkupKind.Line, new(60, 60), new(90, 90));
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 200, 200);
            doc.SetMarkups(0, [a, b], Text);
            doc.SaveAs(path);
            var moved = MarkupGeometry.Translate(a, 100, 100);
            doc.SetMarkups(0, [b, moved], Text);
            doc.Save();
            Assert.Equal(PdfSaveMode.Incremental, PdfSaveMode.Incremental);
        }
        using var reopened = PdfDocument.Open(path);
        var read = reopened.GetMarkups(0);
        Assert.Equal([b.Id, a.Id], read.Select(m => m.Id));
        Assert.Equal(new PdfPoint(110, 110), read[1].Points[0]);
        Assert.Equal(2, AnnotCount(reopened, 0));
    }

    [Fact]
    public void ForeignAnnotationsAreKept()
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 200, 200);
        lock (PdfiumLibrary.Sync)
        {
            var page = Pdfium.FPDF_LoadPage(doc.Handle, 0);
            var annot = Pdfium.FPDFPage_CreateAnnot(page, Pdfium.FPDF_ANNOT_SQUARE);
            var r = new Pdfium.FS_RECTF { left = 1, top = 20, right = 20, bottom = 1 };
            Pdfium.FPDFAnnot_SetRect(annot, &r);
            Pdfium.FPDFPage_CloseAnnot(annot);
            Pdfium.FPDF_ClosePage(page);
        }
        Assert.Empty(doc.GetMarkups(0));
        doc.SetMarkups(0, [Make(MarkupKind.Rectangle, new(50, 50), new(60, 60))], Text);
        doc.SetMarkups(0, [], Text);
        Assert.Equal(1, AnnotCount(doc, 0));
    }

    [Fact]
    public void MarkupMovedInAnotherProgramIsNoLongerManaged()
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 200, 200);
        doc.SetMarkups(0, [Make(MarkupKind.Rectangle, new(50, 50), new(60, 60))], Text);
        var bytes = new MemoryStream();
        doc.WriteTo(bytes, PdfSaveMode.Rewrite);
        // Ein anderes Programm verschiebt die Anmerkung, ohne die EGLE360-Daten anzupassen.
        using var other = PdfDocument.Open(bytes.ToArray());
        lock (PdfiumLibrary.Sync)
        {
            var page = Pdfium.FPDF_LoadPage(other.Handle, 0);
            var annot = Pdfium.FPDFPage_GetAnnot(page, 0);
            var r = new Pdfium.FS_RECTF { left = 100, top = 120, right = 120, bottom = 100 };
            Pdfium.FPDFAnnot_SetRect(annot, &r);
            Pdfium.FPDFPage_CloseAnnot(annot);
            Pdfium.FPDF_ClosePage(page);
        }
        Assert.Empty(other.GetMarkups(0));
    }

    [Fact]
    public void EncryptedDocumentsKeepValidInkAnnotations()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("encrypted.pdf", dir);
        var line = Make(MarkupKind.Line, new(10, 10), new(100, 100));
        using (var doc = PdfDocument.Open(path, "egle360"))
        {
            doc.SetMarkups(0, [line], Text);
            doc.Save();
        }
        using var reopened = PdfDocument.Open(path, "egle360");
        Assert.True(reopened.IsEncrypted);
        Assert.Equal(line.Points, reopened.GetMarkups(0).Single().Points);
        lock (PdfiumLibrary.Sync)
        {
            var page = Pdfium.FPDF_LoadPage(reopened.Handle, 0);
            var annot = Pdfium.FPDFPage_GetAnnot(page, 0);
            Assert.Equal(Pdfium.FPDF_ANNOT_INK, Pdfium.FPDFAnnot_GetSubtype(annot));
            Pdfium.FPDFPage_CloseAnnot(annot);
            Pdfium.FPDF_ClosePage(page);
        }
    }

    [Fact]
    public void MarkupsNeedAnnotatePermission()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("no-modify.pdf"));
        Assert.False(doc.CanAnnotate);
        var ex = Assert.Throws<PdfException>(() => doc.SetMarkups(0, [Make(MarkupKind.Line, new(0, 0), new(10, 10))], Text));
        Assert.Equal(PdfErrorKind.Permission, ex.Kind);
    }

    [Fact]
    public void RotatedPageTransformMatchesPdfium()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        for (var index = 0; index < doc.PageCount; index++)
        {
            var t = doc.GetPageTransform(index);
            var info = doc.GetPageInfo(index);
            Assert.Equal(info.WidthPt, t.DisplayWidth, 1);
            Assert.Equal(info.HeightPt, t.DisplayHeight, 1);
            var p = new PdfPoint(t.Box.Left + 100, t.Box.Bottom + 50);
            var (u, v) = t.ToDisplay(p);
            const int scale = 4;
            lock (PdfiumLibrary.Sync)
            {
                var page = Pdfium.FPDF_LoadPage(doc.Handle, index);
                int dx, dy;
                Pdfium.FPDF_PageToDevice(page, 0, 0, (int)Math.Round(t.DisplayWidth * scale), (int)Math.Round(t.DisplayHeight * scale), 0, p.X, p.Y, &dx, &dy);
                Pdfium.FPDF_ClosePage(page);
                Assert.InRange(dx, u * scale - 2, u * scale + 2);
                Assert.InRange(dy, v * scale - 2, v * scale + 2);
            }
        }
    }

    [Fact]
    public void HistoryUndoesAndRedoes()
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 200, 200);
        var history = new MarkupHistory(doc, Text);
        var a = Make(MarkupKind.Rectangle, new(10, 10), new(50, 50));
        history.Add(0, a);
        var moved = MarkupGeometry.Translate(a, 10, 0);
        history.Replace(0, moved);
        Assert.Equal("Rechteck ändern", history.UndoDescription);
        Assert.Equal(0, history.Undo());
        Assert.Equal(a.Points, doc.GetMarkups(0).Single().Points);
        history.Undo();
        Assert.Empty(doc.GetMarkups(0));
        Assert.False(history.CanUndo);
        history.Redo();
        history.Redo();
        Assert.Equal(moved.Points, doc.GetMarkups(0).Single().Points);
        history.Remove(0, [a.Id]);
        Assert.Empty(doc.GetMarkups(0));
        Assert.False(history.CanRedo);
    }

    [Fact]
    public void StructureChangesClearHistoryAndCache()
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 200, 200);
        doc.InsertBlankPage(1, 300, 300);
        var history = new MarkupHistory(doc, Text);
        history.Add(1, Make(MarkupKind.Rectangle, new(10, 10), new(50, 50)));
        doc.MovePage(1, 0);
        Assert.False(history.CanUndo);
        Assert.Single(doc.GetMarkups(0));
        Assert.Empty(doc.GetMarkups(1));
    }

    private static int AnnotCount(PdfDocument doc, int index)
    {
        lock (PdfiumLibrary.Sync)
        {
            var page = Pdfium.FPDF_LoadPage(doc.Handle, index);
            var count = Pdfium.FPDFPage_GetAnnotCount(page);
            Pdfium.FPDF_ClosePage(page);
            return count;
        }
    }
}

public class PdfObjectPatcherTests
{
    [Fact]
    public void PatchKeepsLengthAndReplacesEntries()
    {
        const string obj = "<</Annots[<</AP<</N 5 0 R >>/C[ 1 0 0]/E360Std(<</Subtype /Line /L [10 10 200 100]>>)/InkList[[ 10 10 200 100]]/NM(e360-a\\)b)/Rect[ 10 10 210 110]/Subtype/Ink/Type/Annot>> <</Subtype/Square/Rect[0 0 1 1]>>]/MediaBox[ 0 0 600 400]/Parent 2 0 R /Type/Page>>";
        var bytes = Encoding.Latin1.GetBytes(obj);
        Assert.Equal(1, PdfObjectPatcher.PatchObject(bytes));
        var result = Encoding.Latin1.GetString(bytes);
        Assert.Equal(obj.Length, result.Length);
        Assert.Contains("/Subtype /Line /L [10 10 200 100]", result);
        Assert.Contains("/NM (e360-a\\)b)", result);
        Assert.Contains("/AP <</N 5 0 R >>", result);
        Assert.Contains("<</Subtype/Square/Rect[0 0 1 1]>>", result);
        Assert.DoesNotContain("InkList", result);
        Assert.DoesNotContain("/Ink", result);
        Assert.DoesNotContain("E360Std", result);
    }

    [Theory]
    [InlineData("<</E360Std(<</Contents (x)>>)/Subtype/Ink>>")] // nicht erlaubter Schlüssel
    [InlineData("<</E360Std(kein Wörterbuch)/Subtype/Ink>>")]
    [InlineData("<</E360Std<4142>/Subtype/Ink>>")]
    public void UnsafePayloadsAreIgnored(string obj)
    {
        var bytes = Encoding.Latin1.GetBytes(obj);
        Assert.Equal(0, PdfObjectPatcher.PatchObject(bytes));
        Assert.Equal(obj, Encoding.Latin1.GetString(bytes));
    }

    [Fact]
    public void MarkerInsideTextIsNotTouched()
    {
        const string obj = "<</Contents(/E360Std\\(<</Subtype /Line>>\\))/Subtype/Ink>>";
        var bytes = Encoding.Latin1.GetBytes(obj);
        Assert.Equal(0, PdfObjectPatcher.PatchObject(bytes));
    }

    [Fact]
    public void LiteralEscapesAreDecoded()
    {
        Assert.Equal("a(b)\n\\A", Encoding.Latin1.GetString(PdfObjectPatcher.DecodeLiteral(Encoding.Latin1.GetBytes("(a\\(b\\)\\n\\\\\\101)"))));
    }
}
