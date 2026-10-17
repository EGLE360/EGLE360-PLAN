using Egle360.Pdf.Compare;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Vergleich zweier Planstände.</summary>
public class CompareTests
{
    private static PdfDocument Plan(bool withExtraWall, int rotation = 0, double ox = 0, double oy = 0)
    {
        var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 600, 400);
        using (var c = doc.EditContent(0))
        {
            c.Rectangle(50 + ox, 50 + oy, 500, 300, PdfColor.Black, null, 2);
            c.Line(50 + ox, 250 + oy, 200 + ox, 250 + oy, PdfColor.Black, 1);
            c.Line(120 + ox, 50 + oy, 120 + ox, 250 + oy, PdfColor.Black, 1);
            c.Text(60 + ox, 360 + oy, "Grundriss", 14, PdfColor.Black);
            if (withExtraWall)
            {
                c.Line(300 + ox, 50 + oy, 300 + ox, 200 + oy, PdfColor.Black, 3);
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

    [Fact]
    public void Verschobener_Plan_wird_ausgerichtet()
    {
        using var a = Plan(false);
        using var b = Plan(true, ox: 17, oy: -9);
        var plain = PageComparer.Compare(a, 0, b, 0, 1);
        var aligned = PageComparer.Compare(a, 0, b, 0, 1, align: true);
        // Ohne Ausrichtung erscheint der ganze Plan als geändert.
        Assert.True(plain.ChangedPixels > 5 * aligned.ChangedPixels, $"{plain.ChangedPixels} / {aligned.ChangedPixels}");
        // Neuer Inhalt liegt 17 pt weiter rechts und 9 pt weiter unten (Pixel nach unten positiv).
        Assert.Equal(17, aligned.OffsetX);
        Assert.Equal(9, aligned.OffsetY);
        var change = Assert.Single(aligned.Changes);
        // Die Wolke gehört in den neuen Stand: Wand bei x = 317, y = 41 bis 191.
        Assert.True(change.Contains(new PdfPoint(317, 110)), change.ToString());

        using var same = Plan(false);
        Assert.Equal((0, 0), (PageComparer.Compare(a, 0, same, 0, 1, align: true).OffsetX, PageComparer.Compare(a, 0, same, 0, 1, align: true).OffsetY));
    }
}
