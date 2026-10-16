using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Egle360.Pdf;
using Egle360.Pdf.Export;
using Egle360.Pdf.Markups;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>Markierungs- und Mengenliste unter dem Plan: alle Markierungen des Dokuments mit Messwerten und Summen.</summary>
public partial class DocumentsView
{
    private const double DefaultListHeight = 220;

    private List<MarkupReportRow> _markupRows = [];
    private bool _syncingMarkupGrid;

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
        var rows = new List<MarkupReportRow>();
        if (_active is { } open)
        {
            for (var page = 0; page < open.Document.PageCount; page++)
            {
                foreach (var m in open.Document.GetMarkups(page))
                {
                    rows.Add(MarkupReport.ToRow(page, m));
                }
            }
        }
        _markupRows = rows;
        ApplyMarkupFilter();
    }

    private void OnMarkupListFilter(object sender, RoutedEventArgs e) => ApplyMarkupFilter();

    private IEnumerable<MarkupReportRow> FilteredRows()
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
        MarkupTotals.Text = MarkupReport.Totals(rows);
    }

    private void OnMarkupRowSelected(object sender, SelectionChangedEventArgs e)
    {
        if (_syncingMarkupGrid || MarkupGrid.SelectedItem is not MarkupReportRow row || _active is not { } open)
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
            Filter = "Excel-Arbeitsmappe (*.xlsx)|*.xlsx|CSV-Datei für Excel (*.csv)|*.csv",
            FileName = $"{baseName} Markierungen.xlsx",
        };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        var rows = FilteredRows().ToList();
        try
        {
            if (Path.GetExtension(dialog.FileName).Equals(".csv", StringComparison.OrdinalIgnoreCase))
            {
                File.WriteAllText(dialog.FileName, MarkupReport.ToCsv(rows), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            else
            {
                File.WriteAllBytes(dialog.FileName, MarkupReport.ToXlsx(rows, $"Mengen aus {baseName}"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"Die Liste konnte nicht gespeichert werden: {ex.Message}");
        }
    }
}
