using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Objektfang an Linien des Planinhalts.</summary>
public class SnapTests
{
    [Fact]
    public void Fangpunkte_aus_Linien_und_Rechtecken()
    {
        using var dir = new TempDir();
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 842, 595);
            using (var c = doc.EditContent(0))
            {
                c.Line(100, 100, 300, 100, PdfColor.Black, 1);
                c.Rectangle(400, 200, 100, 50, PdfColor.Black);
                c.Text(100, 400, "Text liefert keine Fangpunkte", 12, PdfColor.Black);
                c.Commit();
            }
            doc.SaveAs(dir.File("fang.pdf"));
        }
        using var reopened = PdfDocument.Open(dir.File("fang.pdf"));
        var index = reopened.GetSnapIndex(0);
        Assert.True(index.Count >= 6, $"nur {index.Count} Punkte");

        // Endpunkt und Mittelpunkt der Linie, Ecke und Kantenmitte des Rechtecks.
        Assert.Equal(new PdfPoint(300, 100), Round(index.Nearest(new PdfPoint(303, 98), 8)));
        Assert.Equal(new PdfPoint(200, 100), Round(index.Nearest(new PdfPoint(198, 104), 8)));
        Assert.Equal(new PdfPoint(500, 250), Round(index.Nearest(new PdfPoint(497, 252), 8)));
        Assert.Equal(new PdfPoint(450, 200), Round(index.Nearest(new PdfPoint(452, 198), 8)));
        Assert.Null(index.Nearest(new PdfPoint(200, 400), 8));
    }

    [Fact]
    public void Index_findet_den_naechsten_Punkt()
    {
        var index = new SnapIndex([new(0, 0), new(10, 0), new(100, 100), new(23.9, 0)]);
        Assert.Equal(new PdfPoint(10, 0), index.Nearest(new PdfPoint(12, 1), 5));
        Assert.Equal(new PdfPoint(23.9, 0), index.Nearest(new PdfPoint(24.5, 0), 5));
        Assert.Null(index.Nearest(new PdfPoint(50, 50), 5));
        Assert.Null(SnapIndex.Empty.Nearest(new PdfPoint(0, 0), 5));
    }

    private static PdfPoint? Round(PdfPoint? p) => p is { } q ? new PdfPoint(Math.Round(q.X, 2), Math.Round(q.Y, 2)) : null;
}
