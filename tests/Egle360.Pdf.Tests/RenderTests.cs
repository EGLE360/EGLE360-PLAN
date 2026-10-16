namespace Egle360.Pdf.Tests;

/// <summary>
/// Rendering-Tests mit bekannter Geometrie: Jede Seite der Referenzdatei hat ein schwarzes Quadrat
/// von 72 × 72 pt, 36 pt vom linken und unteren Rand.
/// </summary>
public class RenderTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(2.0)]
    [InlineData(96.0 / 72.0)]
    public void SquareAppearsAtExpectedPixels(double scale)
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var (buf, w, h) = Fixtures.RenderFull(doc, 1, scale);
        var stride = w * 4;
        // Mitte des Quadrats: 72 pt vom linken und unteren Rand.
        var cx = (int)(72 * scale);
        var cy = h - (int)(72 * scale);
        Assert.True(Fixtures.Pixel(buf, stride, cx, cy).IsBlack());
        // Knapp außerhalb (rechts oberhalb) ist die Seite weiß.
        var ox = (int)(120 * scale);
        var oy = h - (int)(120 * scale);
        Assert.True(Fixtures.Pixel(buf, stride, ox, oy).IsWhite());
        // Gegenüberliegende Ecke weiß.
        Assert.True(Fixtures.Pixel(buf, stride, w - 5, 5).IsWhite());
    }

    [Fact]
    public void PageRotationIsApplied()
    {
        // Seite 3 ist hochkant gespeichert und um 90 Grad im Uhrzeigersinn gedreht.
        // Die linke untere Ecke des Originals liegt dann oben links.
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var (buf, w, h) = Fixtures.RenderFull(doc, 2, 1.0);
        Assert.True(w > h);
        Assert.True(Fixtures.Pixel(buf, w * 4, 72, 72).IsBlack());
        Assert.True(Fixtures.Pixel(buf, w * 4, 72, h - 72).IsWhite());
    }

    [Fact]
    public void RegionMatchesSameAreaOfFullRender()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        const double scale = 3.0;
        var (full, fw, fh) = Fixtures.RenderFull(doc, 1, scale);

        var region = new PixelRect(60, fh - 400, 300, 250);
        var part = new byte[region.Width * region.Height * 4];
        doc.Render(1, scale, region, part, region.Width * 4);

        var differing = 0;
        for (var y = 0; y < region.Height; y++)
        {
            for (var x = 0; x < region.Width; x++)
            {
                var a = Fixtures.Pixel(full, fw * 4, region.X + x, region.Y + y);
                var b = Fixtures.Pixel(part, region.Width * 4, x, y);
                if (Math.Abs(a.B - b.B) > 8 || Math.Abs(a.G - b.G) > 8 || Math.Abs(a.R - b.R) > 8)
                {
                    differing++;
                }
            }
        }
        Assert.Equal(0, differing);
    }

    [Fact]
    public void HighZoomRendersOnlyTheRegion()
    {
        // A0 bei 6400 %: die ganze Seite hätte über 200 000 Pixel Kantenlänge. Gerendert wird nur der Ausschnitt.
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        const double scale = 64 * 96.0 / 72.0;
        var (fullW, fullH) = doc.PagePixelSize(0, scale);
        Assert.True(fullW > 200_000);

        // Ausschnitt um die linke Kante des Quadrats (x = 36 pt, y = 72 pt).
        var edgeX = (int)(36 * scale);
        var edgeY = fullH - (int)(72 * scale);
        var region = new PixelRect(edgeX - 100, edgeY - 50, 200, 100);
        var buf = new byte[region.Width * region.Height * 4];
        doc.Render(0, scale, region, buf, region.Width * 4);

        Assert.True(Fixtures.Pixel(buf, region.Width * 4, 50, 50).IsWhite());
        Assert.True(Fixtures.Pixel(buf, region.Width * 4, 150, 50).IsBlack());
    }

    [Fact]
    public void ParallelRenderingGivesSameResult()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        var expected = Enumerable.Range(0, doc.PageCount).Select(p => Fixtures.RenderFull(doc, p, 0.5).Buffer).ToArray();
        var results = new byte[16][];
        Parallel.For(0, results.Length, i => results[i] = Fixtures.RenderFull(doc, i % doc.PageCount, 0.5).Buffer);
        for (var i = 0; i < results.Length; i++)
        {
            Assert.Equal(expected[i % doc.PageCount], results[i]);
        }
    }

    [Fact]
    public void TooSmallBufferIsRejected()
    {
        using var doc = PdfDocument.Open(Fixtures.PathOf("reference.pdf"));
        Assert.Throws<ArgumentException>(() => doc.Render(0, 1, new PixelRect(0, 0, 10, 10), new byte[399], 40));
    }
}
