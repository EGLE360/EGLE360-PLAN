namespace Egle360.Pdf.Tests;

/// <summary>Seiten bearbeiten, Zusammenführen, Aufteilen, Neu erstellen und Speichern.</summary>
public class EditAndSaveTests
{
    [Fact]
    public void IncrementalSaveKeepsOriginalBytes()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("reference.pdf", dir);
        var original = File.ReadAllBytes(path);

        using (var doc = PdfDocument.Open(path))
        {
            doc.RotatePage(0, 1);
            Assert.True(doc.IsModified);
            Assert.Equal(PdfSaveMode.Incremental, doc.DefaultSaveMode);
            doc.Save();
            Assert.False(doc.IsModified);
            Assert.Equal(90, doc.GetPageRotation(0));
        }

        var saved = File.ReadAllBytes(path);
        Assert.True(saved.Length > original.Length);
        Assert.Equal(original, saved[..original.Length]);

        using var reopened = PdfDocument.Open(path);
        Assert.Equal(90, reopened.GetPageRotation(0));
        Assert.Equal("GR-1", reopened.GetPageInfo(0).DisplayLabel);
    }

    [Fact]
    public void DeletingPagesRewritesTheFile()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("reference.pdf", dir);
        var original = File.ReadAllBytes(path);

        using (var doc = PdfDocument.Open(path))
        {
            doc.DeletePage(3);
            Assert.Equal(3, doc.PageCount);
            Assert.Equal(PdfSaveMode.Rewrite, doc.DefaultSaveMode);
            doc.Save();
        }

        var saved = File.ReadAllBytes(path);
        Assert.NotEqual(original, saved.Length >= original.Length ? saved[..original.Length] : saved);
        using var reopened = PdfDocument.Open(path);
        Assert.Equal(3, reopened.PageCount);
    }

    [Fact]
    public void MovePageChangesOrder()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        doc.MovePage(0, 2);
        Assert.Equal(["A2", "A4", "A0", "Benutzerdefiniert"], doc.GetPages().Select(p => p.Format.Name));
        doc.MovePage(2, 0);
        Assert.Equal(["A0", "A2", "A4", "Benutzerdefiniert"], doc.GetPages().Select(p => p.Format.Name));
    }

    [Fact]
    public void RotateWrapsAround()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        doc.RotatePage(2, 3);
        Assert.Equal(0, doc.GetPageRotation(2));
        doc.RotatePage(2, -1);
        Assert.Equal(270, doc.GetPageRotation(2));
    }

    [Fact]
    public void InsertBlankPageUsesRequestedSize()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var (w, h) = PaperFormat.IsoSizePoints("A3");
        doc.InsertBlankPage(1, h, w);
        Assert.Equal(5, doc.PageCount);
        var format = doc.GetPageInfo(1).Format;
        Assert.Equal("A3", format.Name);
        Assert.Equal(PageOrientation.Landscape, format.Orientation);
    }

    [Fact]
    public void PagesChangedIsRaised()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var count = 0;
        doc.PagesChanged += (_, _) => count++;
        doc.RotatePage(0, 1);
        doc.DeletePage(1);
        Assert.Equal(2, count);
    }

    [Fact]
    public void MergeAndSplit()
    {
        using var dir = new TempDir();
        using (var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf")))
        using (var other = PdfDocument.Open(Fixtures.PathOf("reference.pdf")))
        {
            doc.ImportPages(other, "1,4", doc.PageCount);
            Assert.Equal(6, doc.PageCount);
            Assert.Equal("A0", doc.GetPageInfo(4).Format.Name);
            Assert.False(doc.GetPageInfo(5).Format.IsStandard);
            doc.SaveAs(dir.File("merged.pdf"));
            Assert.Equal(dir.File("merged.pdf"), doc.FilePath);

            doc.ExtractPages("2-3", dir.File("part.pdf"));
            Assert.Equal(6, doc.PageCount);
        }

        using var merged = PdfDocument.Open(dir.File("merged.pdf"));
        Assert.Equal(6, merged.PageCount);
        using var part = PdfDocument.Open(dir.File("part.pdf"));
        Assert.Equal(["A2", "A4"], part.GetPages().Select(p => p.Format.Name));
    }

    [Fact]
    public void InvalidRangeIsRejected()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        using var other = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        Assert.Throws<PdfException>(() => doc.ImportPages(other, "9", 0));
        Assert.Equal(4, doc.PageCount);
    }

    [Fact]
    public void SaveAsLeavesSourceUntouched()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("reference.pdf", dir);
        var original = File.ReadAllBytes(path);
        using var doc = PdfDocument.Open(path);
        doc.DeletePage(0);
        doc.SaveAs(dir.File("kopie.pdf"));
        Assert.Equal(original, File.ReadAllBytes(path));
        Assert.Equal(3, doc.PageCount);
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void EncryptedDocumentStaysEncryptedAfterSave()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("encrypted.pdf", dir);
        using (var doc = PdfDocument.Open(path, "egle360"))
        {
            doc.RotatePage(0, 1);
            doc.Save();
            Assert.Equal(90, doc.GetPageRotation(0));
        }
        Assert.Throws<PdfException>(() => PdfDocument.Open(path));
        using var reopened = PdfDocument.Open(path, "egle360");
        Assert.Equal(90, reopened.GetPageRotation(0));
    }

    [Fact]
    public void NoTemporaryFilesRemain()
    {
        using var dir = new TempDir();
        var path = Fixtures.WorkingCopy("reference.pdf", dir);
        using var doc = PdfDocument.Open(path);
        doc.RotatePage(1, 1);
        doc.Save();
        Assert.Equal(["reference.pdf"], Directory.GetFiles(dir.Path).Select(Path.GetFileName));
    }

    [Fact]
    public void NewDocumentWithContent()
    {
        using var dir = new TempDir();
        var (w, h) = PaperFormat.IsoSizePoints("A4");
        using (var doc = PdfDocument.CreateNew())
        {
            Assert.Equal(0, doc.PageCount);
            Assert.Null(doc.FilePath);
            Assert.Throws<InvalidOperationException>(doc.Save);
            doc.InsertBlankPage(0, w, h);
            using (var content = doc.EditContent(0))
            {
                content.Rectangle(100, 100, 200, 100, PdfColor.Black, new PdfColor(22, 119, 210));
                content.Line(100, 400, 400, 400, PdfColor.Black, 4);
                content.Text(100, 500, "EGLE360 PLAN Grundriss EG", 24, PdfColor.Black);
                content.Commit();
            }
            doc.SaveAs(dir.File("neu.pdf"));
        }

        using var reopened = PdfDocument.Open(dir.File("neu.pdf"));
        Assert.Equal("A4", reopened.GetPageInfo(0).Format.Name);
        var (buf, pw, ph) = Fixtures.RenderFull(reopened, 0, 1.0);
        var stride = pw * 4;
        var fill = Fixtures.Pixel(buf, stride, 200, ph - 150);
        Assert.InRange(fill.B, 190, 230); // #1677D2: Blau 210
        Assert.InRange(fill.R, 0, 40);
        Assert.True(Fixtures.Pixel(buf, stride, 250, ph - 400).IsBlack());

        var textArea = 0;
        for (var x = 100; x < 400; x++)
        {
            for (var y = ph - 520; y < ph - 495; y++)
            {
                if (!Fixtures.Pixel(buf, stride, x, y).IsWhite())
                {
                    textArea++;
                }
            }
        }
        Assert.True(textArea > 300, $"Text nicht gerendert ({textArea} Pixel)");
    }

    [Fact]
    public void WriteToDoesNotChangeState()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        doc.RotatePage(0, 1);
        using var ms = new MemoryStream();
        doc.WriteTo(ms, PdfSaveMode.Rewrite);
        Assert.True(doc.IsModified);
        using var copy = PdfDocument.Open(ms.ToArray());
        Assert.Equal(90, copy.GetPageRotation(0));
    }
}
