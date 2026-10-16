using System.Text;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Stempel und Bemaßungslinie.</summary>
public class StampDimensionTests
{
    private static readonly ITextOutliner Text = new BlockTextOutliner();
    private const double MeterAt50 = 72 / 25.4 * 1000 / 50;

    [Fact]
    public void Stempel_per_Klick_in_Standardgroesse()
    {
        var style = MarkupStyle.DefaultFor(MarkupKind.Stamp);
        var drawing = new MarkupDrawing(MarkupKind.Stamp, style, "Prüfer", 0, 4, text: "GEPRÜFT");
        drawing.Press(new PdfPoint(300, 400));
        var stamp = drawing.Release(new PdfPoint(300, 400));
        Assert.NotNull(stamp);
        Assert.Equal("GEPRÜFT", stamp!.Text);
        var r = MarkupGeometry.HandleRect(stamp);
        Assert.Equal(StampPreset.DefaultSize.Width, r.Width, 6);
        Assert.Equal(StampPreset.DefaultSize.Height, r.Height, 6);
        Assert.Equal(300, (r.Left + r.Right) / 2, 6);
        Assert.Equal(400, (r.Bottom + r.Top) / 2, 6);
        Assert.True(MarkupGeometry.HitTest(stamp, new PdfPoint(300, 400), 1));
        Assert.Equal(8, MarkupGeometry.Handles(stamp).Count);

        // Die Aufschrift bleibt im Rahmen, auch bei langem Text.
        var longStamp = stamp.WithText("FREIGEGEBEN MIT ANMERKUNGEN");
        var shape = MarkupGeometry.BuildShape(longStamp, Text);
        var textLayer = Assert.Single(shape.Layers, l => l.IsText);
        var tb = textLayer.Path.Bounds();
        Assert.True(tb.Left >= r.Left && tb.Right <= r.Right && tb.Bottom >= r.Bottom && tb.Top <= r.Top, $"{tb} außerhalb {r}");
        Assert.StartsWith("Prüfer, ", MarkupGeometry.StampSubtitle(stamp));
    }

    [Fact]
    public void Stempel_wird_als_Stamp_Anmerkung_gespeichert()
    {
        using var dir = new TempDir();
        var stamp = Markup.Create(MarkupKind.Stamp, [[new(100, 100), new(290, 156)]], MarkupStyle.DefaultFor(MarkupKind.Stamp), "Prüfer", text: "ABGELEHNT");
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 842, 595);
            doc.SetMarkups(0, [stamp], Text);
            doc.SaveAs(dir.File("stempel.pdf"));
        }
        var raw = Encoding.Latin1.GetString(File.ReadAllBytes(dir.File("stempel.pdf")));
        Assert.Contains("/Subtype /Stamp", raw.Replace("/Subtype/Stamp", "/Subtype /Stamp"));
        using var reopened = PdfDocument.Open(dir.File("stempel.pdf"));
        var read = Assert.Single(reopened.GetMarkups(0));
        Assert.Equal(MarkupKind.Stamp, read.Kind);
        Assert.Equal("ABGELEHNT", read.Text);
    }

    [Fact]
    public void Bemassung_misst_und_beschriftet_ueber_der_Linie()
    {
        var s = MeasureScale.FromRatio(50);
        var dim = Markup.Create(MarkupKind.Dimension, [[new(100, 100), new(100 + 4.5 * MeterAt50, 100)]], MarkupStyle.DefaultFor(MarkupKind.Dimension), "Prüfer") with { Scale = s };
        Assert.True(MarkupKinds.IsMeasurement(MarkupKind.Dimension));
        Assert.True(MarkupKinds.NeedsScale(MarkupKind.Dimension));
        Assert.Equal("4,50 m", Measurement.Label(dim));

        // Die Beschriftung (weißes Feld) liegt oberhalb der waagrechten Maßlinie.
        var shape = MarkupGeometry.BuildShape(dim, null);
        var label = shape.Layers.Single(l => l.Fill is { R: 255, G: 255, B: 255 });
        Assert.True(label.Path.Bounds().Bottom > 100);

        // Senkrechte Linie: Beschriftung links davon.
        var vertical = dim.WithPaths([[new(300, 100), new(300, 300)]]);
        var vlabel = MarkupGeometry.BuildShape(vertical, null).Layers.Single(l => l.Fill is { R: 255, G: 255, B: 255 });
        Assert.True(vlabel.Path.Bounds().Right < 300);

        var std = MarkupSerializer.StandardEntries(dim)!;
        Assert.Contains("/LE [/OpenArrow /OpenArrow]", std);
        Assert.Contains("/IT /LineDimension", std);
        Assert.Contains("/Measure", std);
    }
}

/// <summary>Radius und Durchmesser.</summary>
public class CircleMeasureTests
{
    private const double MeterAt50 = 72 / 25.4 * 1000 / 50;

    [Fact]
    public void Radius_und_Durchmesser_mit_Kreisflaeche()
    {
        var s = MeasureScale.FromRatio(50);
        var radius = Markup.Create(MarkupKind.Radius, [[new(200, 200), new(200 + 2 * MeterAt50, 200)]], MarkupStyle.DefaultFor(MarkupKind.Radius), "Prüfer") with { Scale = s };
        Assert.Equal("R 2,00 m", Measurement.Label(radius));
        Assert.Equal("R 2,00 m, Kreisfläche 12,57 m²", Measurement.Describe(radius));
        // Der Kreis reicht um den Mittelpunkt herum.
        var bounds = MarkupGeometry.Bounds(radius);
        Assert.True(bounds.Left < 200 - 2 * MeterAt50 + 1);

        var diameter = radius with { Kind = MarkupKind.Diameter };
        Assert.Equal("Ø 2,00 m", Measurement.Label(diameter));
        Assert.Equal(Math.PI, Measurement.CircleArea(diameter)!.Value, 6);
        Assert.False(MarkupKinds.IsSummable(MarkupKind.Diameter));
        Assert.Contains("/IT /LineDimension", MarkupSerializer.StandardEntries(diameter));
    }
}
