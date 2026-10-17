namespace Egle360.Pdf.Tests;

/// <summary>Seitentext lesen und durchsuchen.</summary>
public class TextSearchTests
{
    private static PdfDocument TwoPages()
    {
        var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 595, 842);
        doc.InsertBlankPage(1, 595, 842);
        using (var c = doc.EditContent(0))
        {
            c.Text(100, 700, "Grundriss EG Wohnung Top 1", 20, PdfColor.Black);
            c.Text(100, 600, "Bad und WC, Lueftung pruefen", 12, PdfColor.Black);
            c.Commit();
        }
        using (var c = doc.EditContent(1))
        {
            c.Text(100, 400, "Schnitt A-A, Wohnung Top 2", 14, PdfColor.Black);
            c.Commit();
        }
        return doc;
    }

    [Fact]
    public void Seitentext_wird_gelesen()
    {
        using var doc = TwoPages();
        var text = doc.GetPageText(0);
        Assert.Contains("Grundriss EG", text);
        Assert.Contains("Lueftung", text);
        Assert.Equal("", PdfDocument.CreateNew().Let(d => { d.InsertBlankPage(0, 100, 100); return d.GetPageText(0); }));
    }

    [Fact]
    public void Suche_ignoriert_Gross_und_Kleinschreibung_und_liefert_Rechtecke()
    {
        using var doc = TwoPages();
        var hits = doc.FindText(0, "wohnung");
        var hit = Assert.Single(hits);
        Assert.Equal(0, hit.Page);
        var r = Assert.Single(hit.Rects);
        // Text beginnt bei x=100, y=700 mit 20 pt; "Wohnung" steht hinter "Grundriss EG ".
        Assert.InRange(r.Left, 150, 300);
        Assert.InRange(r.Bottom, 690, 705);
        Assert.InRange(r.Top, 705, 725);
        Assert.Contains("Wohnung", hit.Context);

        Assert.Single(doc.FindText(1, "WOHNUNG TOP 2"));
        Assert.Empty(doc.FindText(1, "Top 1"));
        Assert.Empty(doc.FindText(0, "   "));
    }

    [Fact]
    public void Mehrere_Fundstellen_auf_einer_Seite()
    {
        using var doc = TwoPages();
        var all = doc.FindText(0, "un");
        Assert.True(all.Count >= 2);
        Assert.True(all.Select(m => m.Start).Distinct().Count() == all.Count);
    }
}

internal static class LetExtension
{
    public static TResult Let<T, TResult>(this T value, Func<T, TResult> f) where T : IDisposable
    {
        using (value)
        {
            return f(value);
        }
    }
}
