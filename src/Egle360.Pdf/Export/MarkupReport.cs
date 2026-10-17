using System.Globalization;
using System.Text;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Export;

/// <summary>
/// Eine Zeile der Markierungs- und Mengenliste. Number ist der Messwert in der Anzeigeeinheit, Derived der Wert aus
/// Höhe oder Tiefe (Volumen in m³, Wandfläche in m²). Cost ist Einheitspreis mal Menge; die Menge ist der abgeleitete
/// Wert, wenn es einen gibt, sonst der geneigte Wert, sonst der Messwert (PriceUnit nennt die Einheit). Slope ist die
/// Neigung in Prozent, Sloped die geneigte Länge oder Fläche, Rise der Höhenunterschied (beide in der Anzeigeeinheit).
/// </summary>
public sealed record MarkupReportRow(
    int PageIndex, string Id, int Page, string Kind, string Value, double? Number, string Unit, double? Perimeter,
    double? Depth, double? Derived, string DerivedUnit, string DerivedName,
    string Text, string Scale, string Author, string Modified, DateTimeOffset ModifiedValue, DateTimeOffset Created, bool IsMeasurement,
    bool Summable = true, string Status = "", string StatusBy = "", DateTimeOffset? StatusAt = null, bool Locked = false,
    double? UnitPrice = null, string PriceUnit = "", double? Cost = null,
    double? Slope = null, double? Sloped = null, string SlopedName = "", double? Rise = null)
{
    /// <summary>Gesamtpreis für die Anzeige, z. B. „1.234,50 €“.</summary>
    public string CostText => Cost is { } c ? $"{Measurement.Number(c, 2)} €" : "";

    /// <summary>Status mit Person für die Anzeige, z. B. „Geprüft (Elias)“.</summary>
    public string StatusText => Status.Length == 0 ? "" : StatusBy.Length == 0 ? Status : $"{Status} ({StatusBy})";
}

/// <summary>Aufbereitung der Markierungen für Liste, Summen und Export (CSV, XLSX).</summary>
public static class MarkupReport
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static MarkupReportRow ToRow(int page, Markup m)
    {
        var v = Measurement.Compute(m);
        var isMeasure = MarkupKinds.IsMeasurement(m.Kind);
        var derived = Measurement.Derived(m);
        var sloped = Measurement.Sloped(m);
        var (quantity, priceUnit) = PricedQuantity(m);
        double? cost = m.UnitPrice is { } price && quantity is { } q ? Math.Round(price * q, 2) : null;
        return new MarkupReportRow(page, m.Id, page + 1, MarkupKinds.DisplayName(m.Kind), isMeasure ? Measurement.Describe(m) : "",
            v?.Value, v?.Unit ?? "", v?.Perimeter, derived is null ? null : m.Depth, derived?.Value, derived?.Unit ?? "", derived?.Name ?? "",
            m.Text.ReplaceLineEndings(" "), m.Scale?.Label ?? "", m.Author, m.Modified.LocalDateTime.ToString("g", German),
            m.Modified, m.Created, isMeasure, MarkupKinds.IsSummable(m.Kind),
            m.Status?.Name ?? "", m.Status?.By ?? "", m.Status?.At, m.Locked, m.UnitPrice, priceUnit, cost,
            sloped is null ? null : m.Slope, sloped?.Value, sloped?.Name ?? "", sloped?.Rise);
    }

    /// <summary>
    /// Menge, auf die sich ein Einheitspreis bezieht: Volumen oder Wandfläche, wenn vorhanden, sonst die geneigte
    /// Länge oder Fläche, sonst der Messwert.
    /// Null bei Markierungen ohne addierbare Menge (Winkel, Bemaßung, Radius, Durchmesser, gewöhnliche Markierungen).
    /// </summary>
    public static (double? Quantity, string Unit) PricedQuantity(Markup m)
    {
        if (!MarkupKinds.IsSummable(m.Kind))
        {
            return (null, "");
        }
        if (Measurement.Derived(m) is { } d)
        {
            return (d.Value, d.Unit);
        }
        if (Measurement.Sloped(m) is { } s)
        {
            return (s.Value, s.Unit);
        }
        return Measurement.Compute(m) is { } v ? (v.Value, v.Unit) : (null, "");
    }

    /// <summary>Summe je Messart und Einheit, dazu Volumen und Wandflächen. Winkel werden nicht summiert.</summary>
    public static IReadOnlyList<(string Name, double Sum, string Unit, int Count)> Sums(IEnumerable<MarkupReportRow> rows)
    {
        var list = rows.ToList();
        var main = list.Where(r => r.Number is not null && r.Summable)
            .GroupBy(r => (r.Kind, r.Unit))
            .Select(g => (g.Key.Kind, g.Sum(r => r.Number!.Value), g.Key.Unit, g.Count()));
        var derived = list.Where(r => r.Derived is not null)
            .GroupBy(r => (r.DerivedName, r.DerivedUnit))
            .Select(g => (g.Key.DerivedName, g.Sum(r => r.Derived!.Value), g.Key.DerivedUnit, g.Count()));
        var sloped = list.Where(r => r.Sloped is not null)
            .GroupBy(r => (r.SlopedName, r.Unit))
            .Select(g => (g.Key.SlopedName, g.Sum(r => r.Sloped!.Value), g.Key.Unit, g.Count()));
        return [.. main, .. sloped, .. derived];
    }

    /// <summary>Summe aller Gesamtpreise; null, wenn keine Zeile einen Preis hat.</summary>
    public static double? TotalCost(IEnumerable<MarkupReportRow> rows)
    {
        var priced = rows.Where(r => r.Cost is not null).ToList();
        return priced.Count == 0 ? null : priced.Sum(r => r.Cost!.Value);
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
        if (TotalCost(rows) is { } cost)
        {
            parts.Add($"Kosten {Measurement.Number(cost, 2)} €");
        }
        var count = rows.Count == 1 ? "1 Markierung" : $"{rows.Count} Markierungen";
        return parts.Count == 0 ? count : $"{count}. Summen: {string.Join(", ", parts)}";
    }

    private static readonly string[] Headers =
        ["Seite", "Art", "Wert", "Einheit", "Umfang", "Neigung (%)", "Geneigt", "Höhenunterschied", "Höhe oder Tiefe (m)", "Volumen oder Wandfläche", "Einheit", "Einheitspreis (€)", "je", "Gesamtpreis (€)",
            "Status", "Status von", "Status am", "Text oder Kommentar", "Maßstab", "Autor", "Erstellt", "Geändert"];

    /// <summary>CSV mit Semikolon und deutschem Zahlenformat, wie Excel es in Österreich und Deutschland erwartet.</summary>
    public static string ToCsv(IReadOnlyList<MarkupReportRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(';', Headers));
        foreach (var r in rows)
        {
            static string Num(double? v) => v is { } x ? x.ToString("0.####", German) : "";
            sb.AppendLine(string.Join(';',
                r.Page.ToString(German), Csv(r.Kind), Num(r.Number), Csv(r.Unit), Num(r.Perimeter), Num(r.Slope), Num(r.Sloped), Num(r.Rise), Num(r.Depth), Num(r.Derived), Csv(r.DerivedUnit),
                Num(r.UnitPrice), Csv(r.UnitPrice is null ? "" : r.PriceUnit), Num(r.Cost),
                Csv(r.Status), Csv(r.StatusBy), r.StatusAt?.LocalDateTime.ToString("dd.MM.yyyy HH:mm", German) ?? "",
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
        var list = new XlsxSheet("Markierungen", [7, 14, 14, 8, 12, 12, 12, 16, 12, 14, 8, 14, 6, 14, 14, 16, 16, 40, 18, 16, 16, 16]);
        list.Rows.Add([.. Headers.Select(h => XlsxCell.Header(h))]);
        foreach (var r in rows)
        {
            list.Rows.Add([
                XlsxCell.Integer(r.Page), XlsxCell.Text(r.Kind), XlsxCell.Number(r.Number), XlsxCell.Text(r.Unit), XlsxCell.Number(r.Perimeter),
                XlsxCell.Number(r.Slope), XlsxCell.Number(r.Sloped), XlsxCell.Number(r.Rise),
                XlsxCell.Number(r.Depth), XlsxCell.Number(r.Derived), XlsxCell.Text(r.DerivedUnit),
                XlsxCell.Number(r.UnitPrice), XlsxCell.Text(r.UnitPrice is null ? "" : r.PriceUnit), XlsxCell.Number(r.Cost),
                XlsxCell.Text(r.Status), XlsxCell.Text(r.StatusBy), r.StatusAt is { } at ? XlsxCell.Date(at.LocalDateTime) : XlsxCell.Text(""),
                XlsxCell.Text(r.Text), XlsxCell.Text(r.Scale),
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
        if (TotalCost(rows) is { } cost)
        {
            sums.Rows.Add([XlsxCell.Text("Kosten"), XlsxCell.Number(cost), XlsxCell.Text("€"), XlsxCell.Integer(rows.Count(r => r.Cost is not null))]);
        }
        return XlsxWriter.Write([list, sums]);
    }
}
