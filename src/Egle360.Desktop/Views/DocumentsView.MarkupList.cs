using System.Globalization;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Egle360.Pdf;
using Egle360.Pdf.Markups;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>Markierungs- und Mengenliste unter dem Plan: alle Markierungen des Dokuments mit Messwerten und Summen.</summary>
public partial class DocumentsView
{
    private const double DefaultListHeight = 220;

    private List<MarkupRow> _markupRows = [];
    private bool _syncingMarkupGrid;

    /// <summary>Eine Zeile der Liste. Number ist der Messwert für die Sortierung.</summary>
    public sealed record MarkupRow(
        int PageIndex, string Id, int Page, string Kind, string Value, double? Number, string Unit, double? Perimeter,
        string Text, string Scale, string Author, string Modified, DateTimeOffset ModifiedValue, DateTimeOffset Created, bool IsMeasurement);

    private bool MarkupListVisible => MarkupListPanel.Visibility == Visibility.Visible;

    private void OnToggleMarkupList(object sender, RoutedEventArgs e)
    {
        var show = !MarkupListVisible;
        MarkupListPanel.Visibility = MarkupListSplitter.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        MarkupListRow.Height = show ? new GridLength(DefaultListHeight) : new GridLength(0);
        MarkupListRow.MinHeight = show ? 120 : 0;
        if (show)
        {
            RefreshMarkupList();
        }
    }

    /// <summary>Für den Screenshot-Modus.</summary>
    internal void SetMarkupListVisible(bool visible)
    {
        if (visible != MarkupListVisible)
        {
            OnToggleMarkupList(this, new RoutedEventArgs());
        }
    }

    /// <summary>Liest die Markierungen aller Seiten neu ein, wenn die Liste sichtbar ist.</summary>
    private void RefreshMarkupList()
    {
        if (!MarkupListVisible)
        {
            return;
        }
        var rows = new List<MarkupRow>();
        if (_active is { } open)
        {
            for (var page = 0; page < open.Document.PageCount; page++)
            {
                foreach (var m in open.Document.GetMarkups(page))
                {
                    rows.Add(ToRow(page, m));
                }
            }
        }
        _markupRows = rows;
        ApplyMarkupFilter();
    }

    private static MarkupRow ToRow(int page, Markup m)
    {
        var v = Measurement.Compute(m);
        var isMeasure = MarkupKinds.IsMeasurement(m.Kind);
        var value = isMeasure ? Measurement.Describe(m) : "";
        return new MarkupRow(page, m.Id, page + 1, MarkupKinds.DisplayName(m.Kind), value, v?.Value, v?.Unit ?? "", v?.Perimeter,
            m.Text.ReplaceLineEndings(" "), m.Scale?.Label ?? "", m.Author, m.Modified.LocalDateTime.ToString("g", CultureInfo.CurrentCulture),
            m.Modified, m.Created, isMeasure);
    }

    private void OnMarkupListFilter(object sender, RoutedEventArgs e) => ApplyMarkupFilter();

    private IEnumerable<MarkupRow> FilteredRows()
    {
        var filter = MarkupFilter?.Text.Trim() ?? "";
        return _markupRows.Where(r =>
            (MeasurementsOnly?.IsChecked != true || r.IsMeasurement)
            && (filter.Length == 0
                || r.Kind.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || r.Text.Contains(filter, StringComparison.CurrentCultureIgnoreCase)
                || r.Author.Contains(filter, StringComparison.CurrentCultureIgnoreCase)));
    }

    private void ApplyMarkupFilter()
    {
        if (MarkupGrid is null)
        {
            return;
        }
        var rows = FilteredRows().ToList();
        _syncingMarkupGrid = true;
        MarkupGrid.ItemsSource = rows;
        _syncingMarkupGrid = false;
        MarkupTotals.Text = Totals(rows);
    }

    /// <summary>Summen je Messart und Einheit, z. B. „Fläche 112,50 m² (3)“.</summary>
    internal static string Totals(IReadOnlyList<MarkupRow> rows)
    {
        var parts = rows.Where(r => r.Number is not null && r.Unit != "°")
            .GroupBy(r => (r.Kind, r.Unit))
            .Select(g =>
            {
                var decimals = g.Key.Unit switch { "Stk." => 0, "m" or "m²" => 2, "cm" or "cm²" => 1, _ => 0 };
                return $"{g.Key.Kind} {Measurement.Number(g.Sum(r => r.Number!.Value), decimals)} {g.Key.Unit} ({g.Count()})";
            })
            .ToList();
        var count = rows.Count == 1 ? "1 Markierung" : $"{rows.Count} Markierungen";
        return parts.Count == 0 ? count : $"{count}. Summen: {string.Join(", ", parts)}";
    }

    private void OnMarkupRowSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMarkupGrid || MarkupGrid.SelectedItem is not MarkupRow row || _active is not { } open)
        {
            return;
        }
        var markup = open.Document.GetMarkups(row.PageIndex).FirstOrDefault(m => m.Id == row.Id);
        if (markup is null)
        {
            return;
        }
        var r = MarkupGeometry.HandleRect(markup);
        open.Viewer.CenterOn(row.PageIndex, new PdfPoint((r.Left + r.Right) / 2, (r.Bottom + r.Top) / 2));
        open.Viewer.Select(row.PageIndex, row.Id);
    }

    private void OnExportMarkups(object sender, RoutedEventArgs e)
    {
        if (_active is not { } open)
        {
            return;
        }
        var baseName = Path.GetFileNameWithoutExtension(open.Document.FilePath ?? open.Title);
        var dialog = new SaveFileDialog
        {
            Filter = "CSV-Datei für Excel (*.csv)|*.csv",
            FileName = $"{baseName} Markierungen.csv",
        };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        try
        {
            File.WriteAllText(dialog.FileName, ToCsv(FilteredRows().ToList()), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"Die Liste konnte nicht gespeichert werden: {ex.Message}");
        }
    }

    /// <summary>CSV mit Semikolon und deutschem Zahlenformat, wie Excel es in Österreich und Deutschland erwartet.</summary>
    internal static string ToCsv(IReadOnlyList<MarkupRow> rows)
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder();
        sb.AppendLine("Seite;Art;Wert;Einheit;Umfang;Text oder Kommentar;Maßstab;Autor;Erstellt;Geändert");
        foreach (var r in rows)
        {
            string Num(double? v) => v is { } x ? x.ToString("0.####", de) : "";
            sb.AppendLine(string.Join(';',
                r.Page.ToString(de), Csv(r.Kind), Num(r.Number), Csv(r.Unit), Num(r.Perimeter), Csv(r.Text), Csv(r.Scale), Csv(r.Author),
                r.Created.LocalDateTime.ToString("dd.MM.yyyy HH:mm", de), r.ModifiedValue.LocalDateTime.ToString("dd.MM.yyyy HH:mm", de)));
        }
        return sb.ToString();
    }

    private static string Csv(string s) =>
        s.IndexOfAny([';', '"', '\n', '\r']) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
}
