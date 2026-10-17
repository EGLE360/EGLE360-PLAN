using Egle360.Pdf.Export;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Neigung und Gefälle bei Längen und Flächen.</summary>
public class SlopeTests
{
    /// <summary>1 m in der Wirklichkeit bei 1:50 in PDF-Punkt.</summary>
    private const double MeterAt50 = 72 / 25.4 * 1000 / 50;

    private static Markup Measure(MarkupKind kind, double? slope, params PdfPoint[] pts) =>
        Markup.Create(kind, [pts], MarkupStyle.DefaultFor(kind), "Prüfer") with { Scale = MeasureScale.FromRatio(50), Slope = slope };

    [Fact]
    public void Rampe_mit_Gefaelle_ergibt_geneigte_Laenge_und_Hoehenunterschied()
    {
        // 4 m im Grundriss bei 75 %: 3 m Höhenunterschied, 5 m geneigt.
        var ramp = Measure(MarkupKind.Length, 75, new(0, 0), new(4 * MeterAt50, 0));
        var s = Measurement.Sloped(ramp)!.Value;
        Assert.Equal(5.0, s.Value, 9);
        Assert.Equal(3.0, s.Rise!.Value, 9);
        Assert.Equal("4,00 m, geneigt 5,00 m", Measurement.Label(ramp));
        Assert.Equal("4,00 m, Neigung 75 % (36,9°), Länge geneigt 5,00 m, Höhenunterschied 3,00 m", Measurement.Describe(ramp));
        Assert.Null(Measurement.Sloped(ramp with { Slope = null }));
    }

    [Fact]
    public void Geneigte_Dachflaeche()
    {
        // 10 m × 6 m Grundriss, Neigung 100 % (45°): Fläche mal √2.
        var roof = Measure(MarkupKind.Area, 100, new(0, 0), new(10 * MeterAt50, 0), new(10 * MeterAt50, 6 * MeterAt50), new(0, 6 * MeterAt50));
        var s = Measurement.Sloped(roof)!.Value;
        Assert.Equal(60 * Math.Sqrt(2), s.Value, 9);
        Assert.Equal("m²", s.Unit);
        Assert.Null(s.Rise);
        Assert.False(Measurement.SupportsSlope(MarkupKind.Count));
        Assert.False(Measurement.SupportsSlope(MarkupKind.Perimeter));
    }

    [Fact]
    public void Neigung_wird_gespeichert_und_in_der_Liste_berechnet()
    {
        var ramp = Measure(MarkupKind.Length, 6, new(0, 0), new(10 * MeterAt50, 0)) with { UnitPrice = 100 };
        var back = MarkupSerializer.FromJson(MarkupSerializer.ToJson(ramp), ramp.Id, ramp.Text, ramp.Author, ramp.Modified)!;
        Assert.Equal(6, back.Slope);

        var row = MarkupReport.ToRow(0, back);
        Assert.Equal(6, row.Slope);
        // Gespeichert wird auf 1/1000 Punkt gerundet, daher nur auf vier Stellen genau.
        Assert.Equal(10 * Math.Sqrt(1.0036), row.Sloped!.Value, 4);
        Assert.Equal(0.6, row.Rise!.Value, 4);
        // Der Preis bezieht sich auf die geneigte Länge.
        Assert.Equal(100 * 10 * Math.Sqrt(1.0036), row.Cost!.Value, 1);
        Assert.Contains(MarkupReport.Sums([row]), x => x.Name == "Länge geneigt");
        Assert.Contains("Neigung (%)", MarkupReport.ToCsv([row]));

        // Unplausible Werte werden beim Lesen verworfen.
        var json = MarkupSerializer.ToJson(ramp with { Slope = 5000 });
        Assert.Null(MarkupSerializer.FromJson(json, ramp.Id, "", "", null)!.Slope);
    }
}
