namespace Egle360.Pdf.Tests;

/// <summary>Öffnen, Seitengrößen, Formate, Seitenlabels, Metadaten, Lesezeichen und Fehlerfälle.</summary>
public class OpenAndReadTests
{
    [Fact]
    public void ReferenceHasFourPages()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        Assert.Equal(4, doc.PageCount);
        Assert.False(doc.IsEncrypted);
        Assert.True(doc.CanModify);
        Assert.False(doc.IsModified);
        Assert.Equal(Path.GetFullPath(Fixtures.PathOf("reference.pdf")), doc.FilePath);
    }

    [Theory]
    [InlineData(0, "A0", PageOrientation.Portrait, 841, 1189)]
    [InlineData(1, "A2", PageOrientation.Landscape, 594, 420)]
    [InlineData(2, "A4", PageOrientation.Landscape, 297, 210)] // hochkant gespeichert, /Rotate 90
    public void IsoFormatsAreRecognised(int page, string name, PageOrientation orientation, double widthMm, double heightMm)
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var format = doc.GetPageInfo(page).Format;
        Assert.True(format.IsStandard);
        Assert.Equal(name, format.Name);
        Assert.Equal(orientation, format.Orientation);
        Assert.Equal(widthMm, format.WidthMm, 0.5);
        Assert.Equal(heightMm, format.HeightMm, 0.5);
    }

    [Fact]
    public void CustomSizeIsNotStandard()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var format = doc.GetPageInfo(3).Format;
        Assert.False(format.IsStandard);
        Assert.Equal("Benutzerdefiniert, 1000 × 707 mm", format.Description);
    }

    [Fact]
    public void RotationIsRead()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        Assert.Equal(0, doc.GetPageRotation(0));
        Assert.Equal(90, doc.GetPageRotation(2));
    }

    [Fact]
    public void PageLabelsAreRead()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        Assert.Equal(["GR-1", "GR-2", "i", "ii"], doc.GetPages().Select(p => p.DisplayLabel));
    }

    [Fact]
    public void MetadataIsRead()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var meta = doc.GetMetadata();
        Assert.Equal("Referenzplan EGLE360", meta.Title);
        Assert.Equal("EGLE360 Testdaten", meta.Author);
        Assert.Equal("Testdatei für PDF-Grundfunktionen", meta.Subject);
        Assert.Equal(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(2)), meta.Created);
        Assert.Null(meta.Keywords);
    }

    [Fact]
    public void BookmarksKeepHierarchyAndTargets()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var bookmarks = doc.GetBookmarks();
        Assert.Equal(2, bookmarks.Count);
        Assert.Equal("Grundriss", bookmarks[0].Title);
        Assert.Equal(0, bookmarks[0].PageIndex);
        Assert.Equal("Schnitt", Assert.Single(bookmarks[0].Children).Title);
        Assert.Equal(1, bookmarks[0].Children[0].PageIndex);
        Assert.Equal("Details", bookmarks[1].Title);
        Assert.Equal(3, bookmarks[1].PageIndex);
    }

    [Fact]
    public void OpenFromMemoryMatchesFile()
    {
        using var doc = PdfDocument.Open(File.ReadAllBytes(Fixtures.PathOf("reference.pdf")));
        Assert.Equal(4, doc.PageCount);
        Assert.Null(doc.FilePath);
    }

    [Fact]
    public void PasswordIsRequired()
    {
        var ex = Assert.Throws<PdfException>(() => PdfDocument.Open(Fixtures.PathOf("encrypted.pdf")));
        Assert.Equal(PdfErrorKind.Password, ex.Kind);
        Assert.Throws<PdfException>(() => PdfDocument.Open(Fixtures.PathOf("encrypted.pdf"), "falsch"));

        using var doc = PdfDocument.Open(Fixtures.PathOf("encrypted.pdf"), "egle360");
        Assert.True(doc.IsEncrypted);
        Assert.Equal(1, doc.PageCount);
    }

    [Fact]
    public void PermissionsAreEnforced()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("no-modify.pdf"));
        Assert.True(doc.IsEncrypted);
        Assert.False(doc.CanModify);
        Assert.True(doc.Permissions.HasFlag(PdfPermissions.Print));
        var ex = Assert.Throws<PdfException>(() => doc.RotatePage(0, 1));
        Assert.Equal(PdfErrorKind.Permission, ex.Kind);
        Assert.False(doc.IsModified);
    }

    [Fact]
    public void InvalidFileReportsFormat()
    {
        var ex = Assert.Throws<PdfException>(() => PdfDocument.Open(Fixtures.PathOf("not-a-pdf.pdf")));
        Assert.Equal(PdfErrorKind.Format, ex.Kind);
    }

    [Fact]
    public void MissingFileReportsFile()
    {
        var ex = Assert.Throws<PdfException>(() => PdfDocument.Open(Fixtures.PathOf("gibt-es-nicht.pdf")));
        Assert.Equal(PdfErrorKind.File, ex.Kind);
    }

    [Fact]
    public void InvalidPageIndexIsRejected()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetPageInfo(4));
        Assert.Throws<ArgumentOutOfRangeException>(() => doc.GetPageInfo(-1));
    }

    [Fact]
    public void DisposedDocumentThrows()
    {
        var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        doc.Dispose();
        Assert.Throws<ObjectDisposedException>(() => doc.PageCount);
        doc.Dispose();
    }
}
