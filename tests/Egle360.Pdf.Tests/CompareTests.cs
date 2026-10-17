using Egle360.Pdf.Compare;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Vergleich zweier Planstände.</summary>
public class CompareTests
{
    private static PdfDocument Plan(bool withExtraWall, int rotation = 0, double ox = 0, double oy = 0, double k = 1)
    {
        var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 600, 400);
        using (var c = doc.EditContent(0))
        {
            c.Rectangle(k * 50 + ox, k * 50 + oy, k * 500, k * 300, PdfColor.Black, null, 2);
            c.Line(k * 50 + ox, k * 250 + oy, k * 200 + ox, k * 250 + oy, PdfColor.Black, 1);
            c.Line(k * 120 + ox, k * 50 + oy, k * 120 + ox, k * 250 + oy, PdfColor.Black, 1);
            c.Text(k * 60 + ox, k * 360 + oy, "Grundriss", 14 * k, PdfColor.Black);
            if (withExtraWall)
            {
                c.Line(k * 300 + ox, k * 50 + oy, k * 300 + ox, k * 200 + oy, PdfColor.Black, 3);
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

    [Fact]
    public void Passpunkte_gleichen_anderen_Massstab_aus()
    {
        using var a = Plan(false);
        using var b = Plan(true, k: 0.9);
        // Ecken des Außenrechtecks in Anzeigekoordinaten (y nach unten, Seitenhöhe 400).
        var alignment = PageAlignment.FromPoints(new PdfPoint(50, 350), new PdfPoint(45, 355), new PdfPoint(550, 50), new PdfPoint(495, 85));
        Assert.NotNull(alignment);
        Assert.Equal(0.9, alignment.Factor, 6);
        Assert.Equal(0, alignment.RotationDegrees, 6);

        var plain = PageComparer.Compare(a, 0, b, 0, 1, align: true);
        var aligned = PageComparer.Compare(a, 0, b, 0, 1, manual: alignment);
        Assert.Same(alignment, aligned.Manual);
        Assert.True(plain.ChangedPixels > 5 * aligned.ChangedPixels, $"{plain.ChangedPixels} / {aligned.ChangedPixels}");
        // Die Wolke gehört in den neuen Stand: Wand bei x = 270, y = 45 bis 180.
        var change = Assert.Single(aligned.Changes);
        Assert.True(change.Contains(new PdfPoint(270, 110)), change.ToString());
        Assert.InRange(change.Width, 3, 40);
    }

    [Fact]
    public void Passpunkte_ergeben_Verschiebung_und_Drehung()
    {
        var shift = PageAlignment.FromPoints(new PdfPoint(10, 20), new PdfPoint(15, 18));
        Assert.True(shift.IsTranslation);
        Assert.Equal((105.0, 98.0), shift.Map(100, 100));

        // Um 90 Grad gedreht und verdoppelt: (0,0) bleibt, (100,0) wird zu (0,200).
        var turned = PageAlignment.FromPoints(new PdfPoint(0, 0), new PdfPoint(0, 0), new PdfPoint(100, 0), new PdfPoint(0, 200))!;
        Assert.Equal(90, turned.RotationDegrees, 6);
        Assert.Equal(2, turned.Factor, 6);
        var (u, v) = turned.Map(0, 50);
        Assert.Equal(-100, u, 6);
        Assert.Equal(0, v, 6);

        // Zu nah beieinander oder unplausibler Maßstab.
        Assert.Null(PageAlignment.FromPoints(new PdfPoint(0, 0), new PdfPoint(0, 0), new PdfPoint(5, 0), new PdfPoint(5, 0)));
        Assert.Null(PageAlignment.FromPoints(new PdfPoint(0, 0), new PdfPoint(0, 0), new PdfPoint(100, 0), new PdfPoint(1000, 0)));
    }

    [Fact]
    public void Kategorien_lassen_sich_ausblenden()
    {
        byte[] Px(PdfColor c) => [c.B, c.G, c.R, 255];
        var overlay = new[] { PageComparer.OldOnly, PageComparer.NewOnly, PageComparer.Same, PdfColor.White }.SelectMany(Px).ToArray();
        var onlyNew = PageComparer.Filter(overlay, showOld: false, showNew: true, showSame: false);
        Assert.Equal([255, 255, 255, 255], onlyNew[0..4]);
        Assert.Equal(Px(PageComparer.NewOnly), onlyNew[4..8]);
        Assert.Equal([255, 255, 255, 255], onlyNew[8..12]);
        Assert.Equal(overlay, PageComparer.Filter(overlay, true, true, true));
        Assert.Equal(Px(PageComparer.OldOnly), overlay[0..4]); // Original bleibt unverändert
    }
}
