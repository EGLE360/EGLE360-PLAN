using System.IO.Compression;
using System.Xml.Linq;
using Egle360.Pdf.Export;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Volumen, Wandflächen und der Export der Mengenliste (CSV, XLSX).</summary>
public class QuantityExportTests
{
    private static readonly ITextOutliner Text = new BlockTextOutliner();
    private const double MeterAt50 = 72 / 25.4 * 1000 / 50;
    private static readonly MeasureScale Scale = MeasureScale.FromRatio(50);

    private static PdfPoint P(double x, double y) => new(x * MeterAt50, y * MeterAt50);

    private static Markup Measure(MarkupKind kind, double? depth, params PdfPoint[] pts) =>
        Markup.Create(kind, [pts], MarkupStyle.DefaultFor(kind), "Prüfer") with { Scale = Scale, Depth = depth };

    [Fact]
    public void Volumen_und_Wandflaeche_aus_Tiefe_und_Hoehe()
    {
        var slab = Measure(MarkupKind.Area, 0.25, P(0, 0), P(10, 0), P(10, 4), P(0, 4));
        var v = Measurement.Derived(slab)!.Value;
        Assert.Equal(10.0, v.Value, 6);
        Assert.Equal("m³", v.Unit);
        Assert.Equal("Volumen", v.Name);
        Assert.Equal("40,00 m², 10,00 m³", Measurement.Label(slab));
        Assert.Equal("40,00 m², Umfang 28,00 m, Volumen 10,00 m³ bei Tiefe 0,25 m", Measurement.Describe(slab));

        var wall = Measure(MarkupKind.Polylength, 2.8, P(0, 0), P(5, 0), P(5, 3));
        Assert.Equal(22.4, Measurement.Derived(wall)!.Value.Value, 6);
        Assert.Equal("Wandfläche", Measurement.Derived(wall)!.Value.Name);

        var room = Measure(MarkupKind.Perimeter, 2.5, P(0, 0), P(4, 0), P(4, 3), P(0, 3));
        Assert.Equal(35.0, Measurement.Derived(room)!.Value.Value, 6);

        Assert.Null(Measurement.Derived(slab with { Depth = null }));
        Assert.Null(Measurement.Derived(slab with { Scale = null }));
        Assert.False(Measurement.SupportsDepth(MarkupKind.Count));
    }

    [Fact]
    public void Tiefe_uebersteht_Speichern()
    {
        using var dir = new TempDir();
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 842, 595);
            doc.SetMarkups(0, [Measure(MarkupKind.Area, 0.3, P(1, 1), P(5, 1), P(5, 5), P(1, 5))], Text);
            doc.SaveAs(dir.File("volumen.pdf"));
        }
        using var reopened = PdfDocument.Open(dir.File("volumen.pdf"));
        var read = Assert.Single(reopened.GetMarkups(0));
        Assert.Equal(0.3, read.Depth!.Value, 9);
        Assert.Equal(4.8, Measurement.Derived(read)!.Value.Value, 3);
    }

    private static List<MarkupReportRow> SampleRows() =>
    [
        MarkupReport.ToRow(0, Measure(MarkupKind.Area, 0.25, P(0, 0), P(10, 0), P(10, 4), P(0, 4))),
        MarkupReport.ToRow(0, Measure(MarkupKind.Area, null, P(0, 0), P(2, 0), P(2, 2), P(0, 2))),
        MarkupReport.ToRow(1, Measure(MarkupKind.Length, 3, P(0, 0), P(6, 0)) with { Text = "Wand; Achse \"B\"" }),
        MarkupReport.ToRow(1, Markup.Create(MarkupKind.Count, [[P(1, 1), P(2, 2)]], MarkupStyle.DefaultFor(MarkupKind.Count), "Prüfer")),
        MarkupReport.ToRow(1, Markup.Create(MarkupKind.Rectangle, [[P(1, 1), P(2, 2)]], MarkupStyle.DefaultFor(MarkupKind.Rectangle), "Prüfer")),
    ];

    [Fact]
    public void Summen_mit_Volumen_und_Wandflaechen()
    {
        var totals = MarkupReport.Totals(SampleRows());
        Assert.Equal("5 Markierungen. Summen: Fläche 44,00 m² (2), Länge 6,00 m (1), Anzahl 2 Stk. (1), Volumen 10,00 m³ (1), Wandfläche 18,00 m² (1)", totals);
    }

    [Fact]
    public void Csv_mit_deutschem_Zahlenformat()
    {
        var lines = MarkupReport.ToCsv(SampleRows()).Split(Environment.NewLine);
        Assert.StartsWith("Seite;Art;Wert;Einheit;Umfang;Neigung (%);Geneigt;Höhenunterschied;Höhe oder Tiefe (m);Volumen oder Wandfläche;Einheit;", lines[0]);
        Assert.StartsWith("1;Fläche;40;m²;28;;;;0,25;10;m³;", lines[1]);
        Assert.Contains(";\"Wand; Achse \"\"B\"\"\";", lines[3]);
    }

    [Fact]
    public void Xlsx_ist_gueltiges_Office_Open_XML()
    {
        var bytes = MarkupReport.ToXlsx(SampleRows(), "Mengen aus Test <A&B>");
        using var zip = new ZipArchive(new MemoryStream(bytes));
        string[] parts = ["[Content_Types].xml", "_rels/.rels", "xl/workbook.xml", "xl/_rels/workbook.xml.rels", "xl/styles.xml", "xl/worksheets/sheet1.xml", "xl/worksheets/sheet2.xml"];
        var docs = parts.ToDictionary(p => p, p =>
        {
            var entry = zip.GetEntry(p);
            Assert.NotNull(entry);
            using var s = entry!.Open();
            return XDocument.Load(s);
        });

        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        var sheets = docs["xl/workbook.xml"].Descendants(ns + "sheet").Select(e => (string)e.Attribute("name")!).ToList();
        Assert.Equal(["Markierungen", "Summen"], sheets);

        var rows = docs["xl/worksheets/sheet1.xml"].Descendants(ns + "row").ToList();
        Assert.Equal(6, rows.Count);
        var first = rows[1].Elements(ns + "c").ToList();
        Assert.Equal("A2", (string)first[0].Attribute("r")!);
        Assert.Equal("1", first[0].Value);
        Assert.Equal("Fläche", first[1].Value);
        Assert.Equal("inlineStr", (string)first[1].Attribute("t")!);
        Assert.Equal(40.0, double.Parse(first[2].Value, System.Globalization.CultureInfo.InvariantCulture), 6);
        Assert.Equal("2", (string)first[2].Attribute("s")!);
        Assert.NotNull(docs["xl/worksheets/sheet1.xml"].Descendants(ns + "autoFilter").SingleOrDefault());

        var sums = docs["xl/worksheets/sheet2.xml"].Descendants(ns + "row").ToList();
        Assert.Equal("Mengen aus Test <A&B>", sums[0].Value);
        Assert.Contains(sums, r => r.Value.StartsWith("Volumen10"));
    }

    [Fact]
    public void Einheitspreise_Status_und_Sperre()
    {
        var at = new DateTimeOffset(2026, 10, 17, 8, 0, 0, TimeSpan.FromHours(2));
        var slab = Measure(MarkupKind.Area, 0.25, P(0, 0), P(10, 0), P(10, 4), P(0, 4)) with
        {
            UnitPrice = 180, // € je m³, weil eine Tiefe angegeben ist
            Status = new MarkupStatus("Geprüft", "Elias", at),
            Locked = true,
        };
        var floor = Measure(MarkupKind.Area, null, P(0, 0), P(2, 0), P(2, 2), P(0, 2)) with { UnitPrice = 45.5 };
        var doors = Markup.Create(MarkupKind.Count, [[P(1, 1), P(2, 2), P(3, 3)]], MarkupStyle.DefaultFor(MarkupKind.Count), "Prüfer") with { UnitPrice = 650 };
        var angle = Markup.Create(MarkupKind.Angle, [[P(1, 0), P(0, 0), P(0, 1)]], MarkupStyle.DefaultFor(MarkupKind.Angle), "Prüfer") with { UnitPrice = 10 };

        Assert.Equal((10.0, "m³"), MarkupReport.PricedQuantity(slab));
        Assert.Equal((null, ""), MarkupReport.PricedQuantity(angle));
        List<MarkupReportRow> rows = [MarkupReport.ToRow(0, slab), MarkupReport.ToRow(0, floor), MarkupReport.ToRow(0, doors), MarkupReport.ToRow(0, angle)];
        Assert.Equal(1800, rows[0].Cost);
        Assert.Equal("1.800,00 €", rows[0].CostText);
        Assert.Equal(182, rows[1].Cost);
        Assert.Equal(1950, rows[2].Cost);
        Assert.Null(rows[3].Cost);
        Assert.Equal("Geprüft (Elias)", rows[0].StatusText);
        Assert.EndsWith("Kosten 3.932,00 €", MarkupReport.Totals(rows));
        Assert.Contains(";180;m³;1800;Geprüft;Elias;17.10.2026 ", MarkupReport.ToCsv(rows).Split(Environment.NewLine)[1]);

        using var dir = new TempDir();
        using (var doc = PdfDocument.CreateNew())
        {
            doc.InsertBlankPage(0, 842, 595);
            doc.SetMarkups(0, [slab, floor], Text);
            doc.SaveAs(dir.File("preise.pdf"));
        }
        using var reopened = PdfDocument.Open(dir.File("preise.pdf"));
        var read = reopened.GetMarkups(0);
        Assert.Equal(180, read[0].UnitPrice);
        Assert.True(read[0].Locked);
        Assert.Equal(new MarkupStatus("Geprüft", "Elias", at), read[0].Status);
        Assert.False(read[1].Locked);
        Assert.Null(read[1].Status);
    }

    [Theory]
    [InlineData(0, "A")]
    [InlineData(25, "Z")]
    [InlineData(26, "AA")]
    [InlineData(701, "ZZ")]
    [InlineData(702, "AAA")]
    public void Spaltennamen(int index, string name) => Assert.Equal(name, XlsxWriter.ColumnName(index));
}
