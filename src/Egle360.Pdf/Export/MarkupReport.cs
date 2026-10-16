using System.Globalization;
using System.Text;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Export;

/// <summary>
/// Eine Zeile der Markierungs- und Mengenliste. Number ist der Messwert in der Anzeigeeinheit, Derived der Wert aus
/// Höhe oder Tiefe (Volumen in m³, Wandfläche in m²).
/// </summary>
public sealed record MarkupReportRow(
    int PageIndex, string Id, int Page, string Kind, string Value, double? Number, string Unit, double? Perimeter,
    double? Depth, double? Derived, string DerivedUnit, string DerivedName,
    string Text, string Scale, string Author, string Modified, DateTimeOffset ModifiedValue, DateTimeOffset Created, bool IsMeasurement);

/// <summary>Aufbereitung der Markierungen für Liste, Summen und Export (CSV, XLSX).</summary>
public static class MarkupReport
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static MarkupReportRow ToRow(int page, Markup m)
    {
        var v = Measurement.Compute(m);
        var isMeasure = MarkupKinds.IsMeasurement(m.Kind);
        var derived = Measurement.Derived(m);
        return new MarkupReportRow(page, m.Id, page + 1, MarkupKinds.DisplayName(m.Kind), isMeasure ? Measurement.Describe(m) : "",
            v?.Value, v?.Unit ?? "", v?.Perimeter, derived is null ? null : m.Depth, derived?.Value, derived?.Unit ?? "", derived?.Name ?? "",
            m.Text.ReplaceLineEndings(" "), m.Scale?.Label ?? "", m.Author, m.Modified.LocalDateTime.ToString("g", German),
            m.Modified, m.Created, isMeasure);
    }

    /// <summary>Summe je Messart und Einheit, dazu Volumen und Wandflächen. Winkel werden nicht summiert.</summary>
    public static IReadOnlyList<(string Name, double Sum, string Unit, int Count)> Sums(IEnumerable<MarkupReportRow> rows)
    {
        var list = rows.ToList();
        var main = list.Where(r => r.Number is not null && r.Unit != "°")
            .GroupBy(r => (r.Kind, r.Unit))
            .Select(g => (g.Key.Kind, g.Sum(r => r.Number!.Value), g.Key.Unit, g.Count()));
        var derived = list.Where(r => r.Derived is not null)
            .GroupBy(r => (r.DerivedName, r.DerivedUnit))
            .Select(g => (g.Key.DerivedName, g.Sum(r => r.Derived!.Value), g.Key.DerivedUnit, g.Count()));
        return [.. main, .. derived];
    }

    public static int DecimalsFor(string unit) => unit switch
    {
        "Stk." => 0,
        "m" or "m²" or "m³" => 2,
        "cm" or "cm²" => 1,
        _ => 0,
    };

    /// <summary>Kurztext der Summen für die Kopfzeile der Liste, z. B. „3 Markierungen. Summen: Fläche 112,50 m² (2)“.</summary>
    public static string Totals(IReadOnlyList<MarkupReportRow> rows)
    {
        var parts = Sums(rows).Select(s => $"{s.Name} {Measurement.Number(s.Sum, DecimalsFor(s.Unit))} {s.Unit} ({s.Count})").ToList();
        var count = rows.Count == 1 ? "1 Markierung" : $"{rows.Count} Markierungen";
        return parts.Count == 0 ? count : $"{count}. Summen: {string.Join(", ", parts)}";
    }

    private static readonly string[] Headers =
        ["Seite", "Art", "Wert", "Einheit", "Umfang", "Höhe oder Tiefe (m)", "Volumen oder Wandfläche", "Einheit", "Text oder Kommentar", "Maßstab", "Autor", "Erstellt", "Geändert"];

    /// <summary>CSV mit Semikolon und deutschem Zahlenformat, wie Excel es in Österreich und Deutschland erwartet.</summary>
    public static string ToCsv(IReadOnlyList<MarkupReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Headers));
        foreach (var r in rows)
        {
            static string Num(double? v) => v is { } x ? x.ToString("0.####", German) : "";
            sb.AppendLine(string.Join(';',
                r.Page.ToString(German), Csv(r.Kind), Num(r.Number), Csv(r.Unit), Num(r.Perimeter), Num(r.Depth), Num(r.Derived), Csv(r.DerivedUnit),
                Csv(r.Text), Csv(r.Scale), Csv(r.Author),
                r.Created.LocalDateTime.ToString("dd.MM.yyyy HH:mm", German), r.ModifiedValue.LocalDateTime.ToString("dd.MM.yyyy HH:mm", German)));
        }
        return sb.ToString();
    }

    private static string Csv(string s) =>
        s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;

    /// <summary>Excel-Mappe mit zwei Blättern: alle Zeilen mit echten Zahlen und Datumswerten, dazu die Summen.</summary>
    public static byte[] ToXlsx(IReadOnlyList<MarkupReportRow> rows, string title)
    {
        var list = new XlsxSheet("Markierungen", [7, 14, 14, 8, 12, 12, 14, 8, 40, 18, 16, 16, 16]);
        list.Rows.Add([.. Headers.Select(h => XlsxCell.Header(h))]);
        foreach (var r in rows)
        {
            list.Rows.Add([
                XlsxCell.Integer(r.Page), XlsxCell.Text(r.Kind), XlsxCell.Number(r.Number), XlsxCell.Text(r.Unit), XlsxCell.Number(r.Perimeter),
                XlsxCell.Number(r.Depth), XlsxCell.Number(r.Derived), XlsxCell.Text(r.DerivedUnit), XlsxCell.Text(r.Text), XlsxCell.Text(r.Scale),
                XlsxCell.Text(r.Author), XlsxCell.Date(r.Created.LocalDateTime), XlsxCell.Date(r.ModifiedValue.LocalDateTime),
            ]);
        }

        var sums = new XlsxSheet("Summen", [24, 16, 10, 10]);
        sums.Rows.Add([XlsxCell.Header(title)]);
        sums.Rows.Add([]);
        sums.Rows.Add([XlsxCell.Header("Art"), XlsxCell.Header("Summe"), XlsxCell.Header("Einheit"), XlsxCell.Header("Anzahl")]);
        foreach (var s in Sums(rows))
        {
            sums.Rows.Add([XlsxCell.Text(s.Name), XlsxCell.Number(s.Sum), XlsxCell.Text(s.Unit), XlsxCell.Integer(s.Count)]);
        }
        return XlsxWriter.Write([list, sums]);
    }
}
