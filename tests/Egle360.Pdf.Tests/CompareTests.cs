using Egle360.Pdf.Compare;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Vergleich zweier Planstände.</summary>
public class CompareTests
{
    private static PdfDocument Plan(bool withExtraWall, int rotation = 0)
    {
        var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 600, 400);
        using (var c = doc.EditContent(0))
        {
            c.Rectangle(50, 50, 500, 300, PdfColor.Black, null, 2);
            c.Text(60, 360, "Grundriss", 14, PdfColor.Black);
            if (withExtraWall)
            {
                c.Line(300, 50, 300, 200, PdfColor.Black, 3);
            }
            c.Commit();
        }
        if (rotation != 0)
        {
            doc.RotatePage(0, rotation / 90);
        }
        return doc;
    }

    [Fact]
    public void Gleiche_Staende_haben_keine_Unterschiede()
    {
        using var a = Plan(false);
        using var b = Plan(false);
        var result = PageComparer.Compare(a, 0, b, 0, 1);
        Assert.False(result.HasChanges);
        Assert.Equal(0, result.ChangedPixels);
        Assert.Equal(600 * 400 * 4, result.Overlay.Length);
    }

    [Fact]
    public void Neue_Wand_wird_gefunden_und_blau_dargestellt()
    {
        using var a = Plan(false);
        using var b = Plan(true);
        var result = PageComparer.Compare(a, 0, b, 0, 1);
        var change = Assert.Single(result.Changes);
        // Die Wand liegt bei x = 300, y = 50 bis 200 (Benutzerkoordinaten).
        Assert.True(change.Contains(new PdfPoint(300, 120)), change.ToString());
        Assert.InRange(change.Width, 3, 40);
        Assert.InRange(change.Bottom, 20, 60);
        Assert.InRange(change.Top, 190, 230);

        // Pixel der Wand: nur im neuen Stand, also blau. Pixelzeile von oben: 400 - 120 = 280.
        var i = (280 * result.Width + 300) * 4;
        Assert.Equal((PageComparer.NewOnly.B, PageComparer.NewOnly.G, PageComparer.NewOnly.R), (result.Overlay[i], result.Overlay[i + 1], result.Overlay[i + 2]));

        // Umgekehrt: im alten Stand vorhanden, im neuen entfernt, also rot.
        var reverse = PageComparer.Compare(b, 0, a, 0, 1);
        Assert.Single(reverse.Changes);
        Assert.Equal(PageComparer.OldOnly.R, reverse.Overlay[i + 2]);

        var clouds = PageComparer.ChangeClouds(result.Changes, "Prüfer", "Änderung gegenüber Index A");
        var cloud = Assert.Single(clouds);
        Assert.Equal(MarkupKind.Cloud, cloud.Kind);
        Assert.Equal("Änderung gegenüber Index A", cloud.Text);
    }

    [Fact]
    public void Bereich_stimmt_auch_auf_gedrehter_Seite()
    {
        using var a = Plan(false, 90);
        using var b = Plan(true, 90);
        var result = PageComparer.Compare(a, 0, b, 0, 1);
        Assert.Equal(400, result.Width);
        var change = Assert.Single(result.Changes);
        Assert.True(change.Contains(new PdfPoint(300, 120)), change.ToString());
    }
}
