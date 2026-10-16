using System.Text;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Maßstab, Messwerte und ihre Speicherung im PDF.</summary>
public class MeasurementTests
{
    private static readonly ITextOutliner Text = new BlockTextOutliner();

    /// <summary>1 m in der Wirklichkeit bei 1:50 in PDF-Punkt.</summary>
    private const double MeterAt50 = 72 / 25.4 * 1000 / 50;

    private static Markup Measure(MarkupKind kind, MeasureScale? scale, params PdfPoint[] pts) =>
        Markup.Create(kind, [pts], MarkupStyle.DefaultFor(kind), "Prüfer") with { Scale = scale };

    [Fact]
    public void Massstab_1_zu_50_rechnet_Papiermass_um()
    {
        var s = MeasureScale.FromRatio(50);
        Assert.Equal("1:50", s.Label);
        Assert.Equal(1.0, s.MetersPerPoint * MeterAt50, 9);
    }

    [Fact]
    public void Kalibrierung_aus_bekannter_Strecke()
    {
        var s = MeasureScale.Calibrated(MeterAt50 * 5, 5, MeasureUnit.Meter);
        Assert.Equal(MeasureScale.FromRatio(50).MetersPerPoint, s.MetersPerPoint, 12);
        Assert.Equal("Kalibriert, etwa 1:50", s.Label);
        Assert.Throws<ArgumentOutOfRangeException>(() => MeasureScale.Calibrated(0, 5, MeasureUnit.Meter));
    }

    [Fact]
    public void Laenge_Flaeche_Umfang_und_Anzahl()
    {
        var s = MeasureScale.FromRatio(50);
        var length = Measure(MarkupKind.Length, s, new(0, 0), new(3 * MeterAt50, 4 * MeterAt50));
        Assert.Equal(5.0, Measurement.Compute(length)!.Value.Value, 9);
        Assert.Equal("5,00 m", Measurement.Label(length));

        var poly = Measure(MarkupKind.Polylength, s, new(0, 0), new(2 * MeterAt50, 0), new(2 * MeterAt50, 1.5 * MeterAt50));
        Assert.Equal("3,50 m", Measurement.Label(poly));

        var square = new PdfPoint[] { new(0, 0), new(4 * MeterAt50, 0), new(4 * MeterAt50, 2.5 * MeterAt50), new(0, 2.5 * MeterAt50) };
        var area = Measure(MarkupKind.Area, s, square);
        var v = Measurement.Compute(area)!.Value;
        Assert.Equal(10.0, v.Value, 9);
        Assert.Equal("m²", v.Unit);
        Assert.Equal(13.0, v.Perimeter!.Value, 9);
        Assert.Equal("10,00 m², Umfang 13,00 m", Measurement.Describe(area));
        Assert.Equal("13,00 m", Measurement.Label(Measure(MarkupKind.Perimeter, s, square)));

        var cm = Measure(MarkupKind.Length, MeasureScale.FromRatio(50, MeasureUnit.Centimeter), new(0, 0), new(1.2345 * MeterAt50, 0));
        Assert.Equal("123,5 cm", Measurement.Label(cm));

        var count = Measure(MarkupKind.Count, null, new(0, 0), new(10, 10), new(20, 20));
        Assert.Equal("3 Stk.", Measurement.Label(count));
        Assert.Equal("kein Maßstab", Measurement.Label(Measure(MarkupKind.Length, null, new(0, 0), new(1, 1))));
        Assert.Equal("1.234,50", Measurement.Number(1234.5, 2));
    }

    [Theory]
    [InlineData("2,5", 2.5)]
    [InlineData("2.5", 2.5)]
    [InlineData("1.250,5", 1250.5)]
    [InlineData(" 12 ", 12)]
    public void Zahlen_in_deutscher_und_englischer_Schreibweise(string text, double expected)
    {
        Assert.True(Measurement.TryParseNumber(text, out var v));
        Assert.Equal(expected, v, 9);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("0")]
    public void Ungueltige_Zahlen(string text) => Assert.False(Measurement.TryParseNumber(text, out _));

    [Fact]
    public void Beschriftung_liegt_im_Rahmen_und_ist_anklickbar()
    {
        var s = MeasureScale.FromRatio(100);
        var m = Measure(MarkupKind.Area, s, new(100, 100), new(300, 100), new(300, 200), new(100, 200));
        var shape = MarkupGeometry.BuildShape(m, Text);
        Assert.Contains(shape.Layers, l => l.IsText);
        var center = MarkupGeometry.Centroid(m.Points);
        Assert.Equal((200.0, 150.0), (center.X, center.Y));
        // Die Fläche ist nicht gefüllt, die Beschriftung in der Mitte trifft trotzdem.
        Assert.True(MarkupGeometry.HitTest(m, center, 1));
        Assert.False(MarkupGeometry.HitTest(m, new PdfPoint(120, 120), 1));
        Assert.Equal(4, MarkupGeometry.Handles(m).Count);
    }

    [Fact]
    public void Zaehlen_zeichnet_Punkte_und_ignoriert_den_Mauszeiger()
    {
        var drawing = new MarkupDrawing(MarkupKind.Count, MarkupStyle.DefaultFor(MarkupKind.Count), "Prüfer", 0, 1);
        foreach (var p in new PdfPoint[] { new(10, 10), new(50, 10), new(90, 10) })
        {
            drawing.Press(p);
            Assert.Null(drawing.Release(p));
        }
        drawing.Move(new PdfPoint(200, 200));
        Assert.Equal("3 Stk.", Measurement.Label(drawing.Preview!));
        var done = drawing.Finish()!;
        Assert.Equal(3, done.Points.Count);
        Assert.Null(done.Scale);
    }

    [Fact]
    public void Messung_uebernimmt_Massstab_beim_Zeichnen()
    {
        var s = MeasureScale.FromRatio(50);
        var drawing = new MarkupDrawing(MarkupKind.Length, MarkupStyle.DefaultFor(MarkupKind.Length), "Prüfer", 0, 1, s);
        drawing.Press(new PdfPoint(0, 0));
        drawing.Move(new PdfPoint(MeterAt50 * 2, 0));
        var m = drawing.Release(new PdfPoint(MeterAt50 * 2, 0))!;
        Assert.Equal(s, m.Scale);
        Assert.Equal("2,00 m", Measurement.Label(m));

        var plain = new MarkupDrawing(MarkupKind.Line, MarkupStyle.DefaultFor(MarkupKind.Line), "Prüfer", 0, 1, s);
        plain.Press(new PdfPoint(0, 0));
        Assert.Null(plain.Release(new PdfPoint(50, 0))!.Scale);
    }

    [Fact]
    public void Messungen_und_Seitenmassstab_ueberstehen_Speichern()
    {
        using var dir = new TempDir();
        var path = dir.File("messung.pdf");
        var s = MeasureScale.FromRatio(50);
        var length = Measure(MarkupKind.Length, s, new(100, 100), new(100 + 3 * MeterAt50, 100));
        var area = Measure(MarkupKind.Area, s, new(100, 300), new(300, 300), new(300, 500), new(100, 500));
        var count = Measure(MarkupKind.Count, null, new(400, 100), new(420, 100));
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 842, 595);
            doc.InsertBlankPage(1, 842, 595);
            Assert.Null(doc.GetPageScale(0));
            doc.SetPageScale(0, s);
            doc.SetMarkups(0, [length, area, count], Text);
            doc.SetPageScale(1, MeasureScale.FromRatio(100, MeasureUnit.Centimeter));
            doc.SetPageScale(1, null);
            doc.SaveAs(path);
        }

        var raw = Encoding.Latin1.GetString(File.ReadAllBytes(path));
        Assert.Contains("/IT /LineDimension", raw);
        Assert.Contains("/IT /PolygonDimension", raw);
        Assert.Contains("/Measure <</Type /Measure /Subtype /RL /R (1:50)", raw);
        Assert.Contains("/U (m\\262)", raw);
        Assert.DoesNotContain("/" + MarkupSerializer.StandardKey, raw);

        using var reopened = PdfDocument.Open(path);
        Assert.Equal(s, reopened.GetPageScale(0));
        Assert.Null(reopened.GetPageScale(1));
        var markups = reopened.GetMarkups(0);
        Assert.Equal(3, markups.Count);
        Assert.Equal("3,00 m", Measurement.Label(markups[0]));
        Assert.Equal(s, markups[1].Scale);
        Assert.Equal(2, markups[2].Points.Count);

        // Der verborgene Maßstab bleibt beim Neuschreiben der Markierungen erhalten.
        reopened.SetMarkups(0, [markups[0]], Text);
        reopened.SaveAs(dir.File("messung2.pdf"));
        using var again = PdfDocument.Open(dir.File("messung2.pdf"));
        Assert.Equal(s, again.GetPageScale(0));
        Assert.Single(again.GetMarkups(0));
    }

    [Fact]
    public void Umschalttaste_rastet_ein()
    {
        var a = new PdfPoint(100, 100);
        var h = MarkupDrawing.Constrain(MarkupKind.Length, a, new PdfPoint(200, 108));
        Assert.Equal(200, h.X, 6);
        Assert.Equal(100, h.Y, 6);
        var d = MarkupDrawing.Constrain(MarkupKind.Line, a, new PdfPoint(150, 155));
        Assert.Equal(d.X - 100, d.Y - 100, 6);
        var sq = MarkupDrawing.Constrain(MarkupKind.Rectangle, a, new PdfPoint(140, 70));
        Assert.Equal((140.0, 60.0), (sq.X, sq.Y));

        var drawing = new MarkupDrawing(MarkupKind.Area, MarkupStyle.DefaultFor(MarkupKind.Area), "Prüfer", 0, 1, MeasureScale.FromRatio(50));
        Assert.Null(drawing.Anchor);
        drawing.Press(new PdfPoint(10, 10));
        drawing.Release(new PdfPoint(10, 10));
        Assert.Equal(new PdfPoint(10, 10), drawing.Anchor);
    }

    [Fact]
    public void Flaeche_mit_Ausschnitt()
    {
        var s = MeasureScale.FromRatio(50);
        PdfPoint P(double x, double y) => new(x * MeterAt50, y * MeterAt50);
        IReadOnlyList<PdfPoint> outer = [P(0, 0), P(10, 0), P(10, 10), P(0, 10)];
        IReadOnlyList<PdfPoint> hole = [P(2, 2), P(4, 2), P(4, 4), P(2, 4)];
        var area = Markup.Create(MarkupKind.Area, [outer, hole], MarkupStyle.DefaultFor(MarkupKind.Area) with { Fill = new PdfColor(200, 220, 255) }, "Prüfer") with { Scale = s };
        Assert.Equal(96.0, Measurement.Compute(area)!.Value.Value, 6);
        Assert.Equal("96,00 m², Umfang 40,00 m, 1 Ausschnitt abgezogen", Measurement.Describe(area));

        Assert.Equal(8, MarkupGeometry.Handles(area).Count);
        var moved = MarkupGeometry.DragHandle(area, 5, P(5, 2));
        Assert.Equal(P(5, 2), moved.Paths[1][1]);
        Assert.Equal(outer, moved.Paths[0]);
        Assert.Equal(95.0, Measurement.Compute(moved)!.Value.Value, 6);

        // Im Ausschnitt trifft ein Klick die gefüllte Fläche nicht.
        Assert.False(MarkupGeometry.HitTest(area, P(3, 3), 0.5));
        Assert.True(MarkupGeometry.HitTest(area, P(7, 7), 0.5));
        Assert.Contains("B*", MarkupSerializer.AppearanceStream(MarkupGeometry.BuildShape(area, null)));

        using var dir = new TempDir();
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 842, 595);
            doc.SetMarkups(0, [area], Text);
            doc.SaveAs(dir.File("ausschnitt.pdf"));
        }
        using var reopened = PdfDocument.Open(dir.File("ausschnitt.pdf"));
        var read = Assert.Single(reopened.GetMarkups(0));
        Assert.Equal(2, read.Paths.Count);
        Assert.Equal(96.0, Measurement.Compute(read)!.Value.Value, 3);
    }
}
