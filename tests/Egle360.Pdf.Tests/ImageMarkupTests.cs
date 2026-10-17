using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Bildmarkierungen: Lage, Speicherung im PDF, Darstellung und Werkzeugbibliothek.</summary>
public class ImageMarkupTests
{
    private static readonly ITextOutliner Text = new BlockTextOutliner();

    /// <summary>2 × 2 Pixel: oben links rot, unten links blau, rechte Spalte durchsichtig.</summary>
    private static MarkupImage Sample() => new(2, 2,
    [
        0, 0, 255, 255, 0, 0, 0, 0,
        255, 0, 0, 255, 0, 0, 0, 0,
    ]);

    private static Markup Image(PdfPoint a, PdfPoint b, int rotation = 0) =>
        Markup.Create(MarkupKind.Image, [new[] { a, b }], MarkupStyle.DefaultFor(MarkupKind.Image) with { LineWidth = 0 }, "Prüfer", rotation)
            with { Image = Sample() };

    [Fact]
    public void Bild_wird_ins_PDF_geschrieben_und_aufrecht_dargestellt()
    {
        using var dir = new TempDir();
        var path = dir.File("bild.pdf");
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 200, 200);
            doc.SetMarkups(0, [Image(new(50, 50), new(150, 150))], Text);
            doc.SaveAs(path);
        }
        using var reopened = PdfDocument.Open(path);
        var read = Assert.Single(reopened.GetMarkups(0));
        Assert.Equal(MarkupKind.Image, read.Kind);
        Assert.Equal(Sample().Bgra, read.Image!.Bgra);

        // Darstellung durch PDFium (wie in anderen Programmen): Pixel nach unten gezählt.
        var buffer = new byte[200 * 200 * 4];
        reopened.Render(0, 1, new PixelRect(0, 0, 200, 200), buffer, 800, hideMarkups: false);
        var top = Fixtures.Pixel(buffer, 800, 75, 75);
        var bottom = Fixtures.Pixel(buffer, 800, 75, 125);
        Assert.True(top.R > 200 && top.B < 60, top.ToString());
        Assert.True(bottom.B > 200 && bottom.R < 60, bottom.ToString());
        Assert.True(Fixtures.Pixel(buffer, 800, 125, 75).IsWhite());
    }

    [Fact]
    public void Bild_behaelt_Seitenverhaeltnis_und_folgt_der_Seitendrehung()
    {
        var wide = new MarkupImage(4, 2, new byte[32]);
        // Rahmen 100 × 100: das Bild füllt die Breite, Höhe 50, mittig.
        var plain = MarkupGeometry.ImagePlacement(wide, new PdfRect(0, 0, 100, 100), 0);
        Assert.Equal(new PdfRect(0, 25, 100, 75), plain.Bounds);
        Assert.Equal(new PdfPoint(0, 75), plain.Map(0, 1)); // Bildoberkante links oben

        // Seite um 90° gedreht: Bildoberkante zeigt in Benutzerkoordinaten nach links.
        var turned = MarkupGeometry.ImagePlacement(wide, new PdfRect(0, 0, 100, 100), 90);
        Assert.Equal(new PdfRect(25, 0, 75, 100), turned.Bounds);
        var up = turned.Map(0, 1) - turned.Map(0, 0);
        Assert.True(up.X < 0 && Math.Abs(up.Y) < 1e-9, up.ToString());
    }

    [Fact]
    public void Fehlerhafte_Bilddaten_werden_verworfen()
    {
        var m = Image(new(0, 0), new(10, 10));
        var json = MarkupSerializer.ToJson(m);
        Assert.NotNull(MarkupSerializer.FromJson(json, m.Id, "", "", null)!.Image);
        var broken = json.Replace("\"w\":2", "\"w\":3");
        Assert.Null(MarkupSerializer.FromJson(broken, m.Id, "", "", null)!.Image);
        Assert.False(MarkupImage.IsValid(MarkupImage.MaxSide + 1, 1, new byte[(MarkupImage.MaxSide + 1) * 4]));
    }

    [Fact]
    public void Bildwerkzeug_in_der_Bibliothek()
    {
        var tool = ToolPreset.FromMarkup(Image(new(0, 0), new(10, 10)), "Unterschrift Prüfer");
        var library = new ToolLibrary([new ToolSet("Unterschriften", [tool])]);
        var back = ToolLibrary.FromJson(library.ToJson()).Sets[0].Tools[0];
        Assert.Equal(Sample().Bgra, back.Image!.Bgra);
        Assert.Contains("2 × 2 Pixel", back.Summary);

        var drawn = new MarkupDrawing(MarkupKind.Image, MarkupStyle.DefaultFor(MarkupKind.Image), "Prüfer", 0, 2, image: back.Image);
        drawn.Press(new PdfPoint(100, 100));
        var placed = drawn.Release(new PdfPoint(100, 100))!;
        // Per Klick in Standardgröße (längere Seite 150 pt), mittig auf dem Klickpunkt.
        Assert.Equal(new PdfRect(25, 25, 175, 175), PdfRect.FromPoints(placed.Points[0], placed.Points[1]));
        Assert.Same(back.Image, placed.Image);

        var none = new MarkupDrawing(MarkupKind.Image, MarkupStyle.DefaultFor(MarkupKind.Image), "Prüfer", 0, 2);
        none.Press(new PdfPoint(100, 100));
        Assert.Null(none.Release(new PdfPoint(100, 100)));
    }
}
