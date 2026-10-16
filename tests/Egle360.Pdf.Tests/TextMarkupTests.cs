using System.Text;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Hervorheben, Unterstreichen und Durchstreichen von Text.</summary>
public class TextMarkupTests
{
    private static PdfDocument WithText()
    {
        var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 595, 842);
        using (var c = doc.EditContent(0))
        {
            c.Text(100, 700, "Wand in Stahlbeton", 20, PdfColor.Black);
            c.Text(100, 670, "Dicke laut Statik", 20, PdfColor.Black);
            c.Commit();
        }
        return doc;
    }

    private static Markup FromRects(MarkupKind kind, IEnumerable<PdfRect> rects) =>
        Markup.Create(kind, [.. rects.Select(r => (IReadOnlyList<PdfPoint>)[new PdfPoint(r.Left, r.Bottom), new PdfPoint(r.Right, r.Top)])],
            MarkupStyle.DefaultFor(kind), "Prüfer");

    [Fact]
    public void Textrechtecke_je_Zeile_im_Bereich()
    {
        using var doc = WithText();
        var both = doc.GetTextRects(0, PdfRect.FromPoints(new(90, 660), new(400, 730)));
        Assert.Equal(2, both.Count);
        Assert.True(both[0].Bottom > both[1].Bottom, "Lesereihenfolge: obere Zeile zuerst");
        Assert.InRange(both[0].Left, 95, 105);

        // Nur die erste Zeile, und nur ein Teil davon.
        var part = Assert.Single(doc.GetTextRects(0, PdfRect.FromPoints(new(90, 695), new(160, 725))));
        Assert.True(part.Right < both[0].Right);

        Assert.Empty(doc.GetTextRects(0, PdfRect.FromPoints(new(300, 100), new(400, 200))));
    }

    [Fact]
    public void Unterstreichen_zeichnet_je_Zeile_eine_Linie_und_trifft_den_Text()
    {
        using var doc = WithText();
        var rects = doc.GetTextRects(0, PdfRect.FromPoints(new(90, 660), new(400, 730)));
        var m = FromRects(MarkupKind.Underline, rects);
        var shape = MarkupGeometry.BuildShape(m, new BlockTextOutliner());
        var lines = Assert.Single(shape.Layers, l => l.Stroke is not null);
        Assert.Equal(2, lines.Path.Flatten().Count());
        var center = new PdfPoint((rects[0].Left + rects[0].Right) / 2, (rects[0].Bottom + rects[0].Top) / 2);
        Assert.True(MarkupGeometry.HitTest(m, center, 0.5));
        Assert.Empty(MarkupGeometry.Handles(m));
    }

    [Fact]
    public void Textmarkierungen_ueberstehen_Speichern_mit_QuadPoints()
    {
        using var dir = new TempDir();
        var path = Path.Combine(dir.Path, "text.pdf");
        string[] ids;
        using (var doc = WithText())
        {
            var rects = doc.GetTextRects(0, PdfRect.FromPoints(new(90, 660), new(400, 730)));
            List<Markup> markups = [FromRects(MarkupKind.Highlight, rects), FromRects(MarkupKind.Underline, rects), FromRects(MarkupKind.StrikeOut, rects.Take(1))];
            ids = [.. markups.Select(m => m.Id)];
            doc.SetMarkups(0, markups, new BlockTextOutliner());
            doc.SaveAs(path);
        }
        var raw = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert.Contains("/Underline", raw);
        Assert.Contains("/StrikeOut", raw);
        Assert.Contains("/QuadPoints", raw);

        using var reopened = PdfDocument.Open(path);
        var read = reopened.GetMarkups(0);
        Assert.Equal(ids, read.Select(m => m.Id));
        Assert.Equal([MarkupKind.Highlight, MarkupKind.Underline, MarkupKind.StrikeOut], read.Select(m => m.Kind));
        Assert.Equal(2, read[1].Paths.Count);
        Assert.Single(read[2].Paths);
    }
}
