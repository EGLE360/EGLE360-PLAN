using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Drehen von Markierungen.</summary>
public class RotationTests
{
    private static Markup Make(MarkupKind kind, params PdfPoint[] pts) => Markup.Create(kind, [pts], MarkupStyle.DefaultFor(kind), "Prüfer");

    [Fact]
    public void Linie_dreht_im_Uhrzeigersinn_um_den_Mittelpunkt()
    {
        var line = Make(MarkupKind.Line, new(0, 0), new(10, 0));
        var turned = MarkupRotation.RotateQuarter(line, 1, new PdfPoint(0, 0));
        // Im Uhrzeigersinn zeigt die Linie danach nach unten (y nach oben im PDF).
        Assert.Equal(new PdfPoint(0, -10), turned.Points[1]);
        Assert.Equal(line.Points, MarkupRotation.RotateQuarter(turned, 3, new PdfPoint(0, 0)).Points);
        Assert.Equal(line.Points, MarkupRotation.RotateQuarter(line, 4, new PdfPoint(0, 0)).Points);
    }

    [Fact]
    public void Rechteck_bleibt_Rechteck_und_tauscht_Breite_und_Hoehe()
    {
        var rect = Make(MarkupKind.Rectangle, new(0, 0), new(40, 10));
        var turned = MarkupRotation.RotateQuarter(rect, 1, MarkupRotation.Center([rect]));
        var r = MarkupGeometry.HandleRect(turned);
        Assert.Equal(10, r.Width, 9);
        Assert.Equal(40, r.Height, 9);
        Assert.Equal(20, (r.Left + r.Right) / 2, 9);
        Assert.Equal(5, (r.Bottom + r.Top) / 2, 9);
        Assert.Equal(turned.Points[0], new PdfPoint(r.Left, r.Bottom));
        Assert.Equal(0, turned.TextRotation);
    }

    [Fact]
    public void Text_dreht_mit_Messbeschriftung_nicht()
    {
        var box = Make(MarkupKind.TextBox, new(0, 0), new(100, 20)).WithText("Hinweis");
        Assert.Equal(270, MarkupRotation.RotateQuarter(box, 1, new PdfPoint(50, 10)).TextRotation);
        Assert.Equal(90, MarkupRotation.RotateQuarter(box, 3, new PdfPoint(50, 10)).TextRotation);
        Assert.Equal(180, MarkupRotation.RotateQuarter(box, 2, new PdfPoint(50, 10)).TextRotation);

        var length = Make(MarkupKind.Length, new(0, 0), new(100, 0)).WithScale(MeasureScale.FromRatio(50));
        var turned = MarkupRotation.RotateQuarter(length, 1, new PdfPoint(50, 0));
        Assert.Equal(0, turned.TextRotation);
        Assert.Equal(Measurement.Compute(length)!.Value.Value, Measurement.Compute(turned)!.Value.Value, 9);
    }

    [Fact]
    public void Freies_Drehen_nur_fuer_Punktarten_Flaeche_bleibt_gleich()
    {
        var area = Make(MarkupKind.Area, new(0, 0), new(30, 0), new(30, 20), new(0, 20)).WithScale(MeasureScale.FromRatio(100));
        var turned = MarkupRotation.RotateFree(area, 30, MarkupRotation.Center([area]));
        Assert.NotEqual(area.Points, turned.Points);
        Assert.Equal(Measurement.Compute(area)!.Value.Value, Measurement.Compute(turned)!.Value.Value, 6);
        Assert.Equal(area.Points[0].X, MarkupRotation.RotateFree(turned, -30, MarkupRotation.Center([area])).Points[0].X, 6);

        var rect = Make(MarkupKind.Rectangle, new(0, 0), new(10, 10));
        Assert.Same(rect, MarkupRotation.RotateFree(rect, 30, new PdfPoint(5, 5)));
        Assert.False(MarkupRotation.CanRotateFreely(MarkupKind.Stamp));
        Assert.False(MarkupRotation.CanRotate(MarkupKind.Underline));
    }

    [Fact]
    public void Sprechblase_dreht_Rahmen_und_Pfeilspitze()
    {
        var callout = Markup.Create(MarkupKind.Callout, [[new(100, 100), new(200, 140)], [new(50, 50)]],
            MarkupStyle.DefaultFor(MarkupKind.Callout), "Prüfer", text: "Achtung");
        var center = new PdfPoint(150, 120);
        var turned = MarkupRotation.RotateQuarter(callout, 2, center);
        Assert.Equal(new PdfPoint(250, 190), turned.Paths[1][0]);
        var box = MarkupGeometry.HandleRect(turned);
        Assert.Equal(new PdfRect(100, 100, 200, 140), box);
        Assert.Equal(180, turned.TextRotation);
    }
}
