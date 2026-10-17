using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Egle360.Pdf;
using Egle360.Pdf.Compare;
using Egle360.Pdf.Markups;
using Egle360.Pdf.Plans;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>
/// Vergleich eines ganzen Plansatzes: je Plan der aktuelle gegen den vorherigen Index. Ergebnisliste mit Anzahl der
/// geänderten Bereiche, Änderungswolken für alle Pläne auf einmal und Änderungsbericht als Excel-Mappe.
/// </summary>
public sealed class PlanSetCompareWindow : Window
{
    private readonly PlanProject _project;
    private readonly IReadOnlyList<PlanEntry> _plans;
    private readonly Action<PlanEntry> _openSingle;
    private readonly Func<string, int, IReadOnlyList<Markup>, bool> _addToOpen;
    private readonly ObservableCollection<Row> _rows = [];
    private readonly ProgressBar _progress = new() { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 8, 0, 0) };
    private readonly TextBlock _status;
    private readonly Button _clouds;
    private readonly Button _export;
    private readonly Button _cancel;
    private readonly CancellationTokenSource _stop = new();
    private readonly List<PlanComparisonResult> _results = [];

    /// <summary>Zeile der Ergebnisliste.</summary>
    public sealed record Row(PlanComparisonResult Result)
    {
        public string Number => Result.Plan.Number;

        public string Title => Result.Plan.Title;

        public string Indexes => $"{PlanProject.Display(Result.Old.Index)} zu {PlanProject.Display(Result.New.Index)}";

        public int Changes => Result.TotalChanges;

        public string Summary => Result.Summary;
    }

    /// <param name="openSingle">Öffnet den Einzelvergleich eines Plans (Doppelklick).</param>
    /// <param name="addToOpen">Trägt Wolken in ein schon geöffnetes Dokument ein; false, wenn es nicht geöffnet ist.</param>
    public PlanSetCompareWindow(Window owner, PlanProject project, IReadOnlyList<PlanEntry> plans, Action<PlanEntry> openSingle,
        Func<string, int, IReadOnlyList<Markup>, bool> addToOpen)
    {
        Owner = owner;
        Title = "Plansatz vergleichen";
        Width = Math.Min(1100, owner.ActualWidth * 0.9);
        Height = Math.Min(700, owner.ActualHeight * 0.9);
        MinWidth = 700;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Surface1Brush");
        _project = project;
        _plans = PlanSetComparer.Comparable(plans);
        _openSingle = openSingle;
        _addToOpen = addToOpen;

        var head = new StackPanel();
        head.Children.Add(new TextBlock { Text = "Plansatz vergleichen", Style = (Style)FindResource("TextSubtitle") });
        head.Children.Add(new TextBlock
        {
            Text = "Je Plan wird der aktuelle Stand mit dem vorherigen Index verglichen, Seite für Seite und mit automatischer Ausrichtung. "
                + "Pläne mit nur einem Stand fehlen in der Liste. Doppelklick öffnet den Vergleich eines Plans.",
            Style = (Style)FindResource("TextSecondary"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 4, 0, 0),
        });
        _status = new TextBlock { Style = (Style)FindResource("TextSecondary"), Margin = new Thickness(0, 8, 0, 0), TextWrapping = TextWrapping.Wrap };
        head.Children.Add(_progress);
        head.Children.Add(_status);

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            HeadersVisibility = DataGridHeadersVisibility.Column,
            GridLinesVisibility = DataGridGridLinesVisibility.Horizontal,
            SelectionMode = DataGridSelectionMode.Single,
            CanUserResizeRows = false,
            BorderThickness = new Thickness(0, 1, 0, 0),
            Background = System.Windows.Media.Brushes.Transparent,
            RowBackground = System.Windows.Media.Brushes.Transparent,
            AlternatingRowBackground = System.Windows.Media.Brushes.Transparent,
            ColumnHeaderStyle = (Style)FindResource("ListHeader"),
            CellStyle = (Style)FindResource("ListCell"),
            RowStyle = (Style)FindResource("ListRow"),
            ItemsSource = _rows,
            Margin = new Thickness(0, 12, 0, 0),
        };
        grid.SetResourceReference(DataGrid.BorderBrushProperty, "BorderBrush");
        grid.SetResourceReference(DataGrid.HorizontalGridLinesBrushProperty, "BorderBrush");
        DataGridTextColumn Column(string header, string path, DataGridLength width) =>
            new() { Header = header, Binding = new Binding(path), Width = width };
        grid.Columns.Add(Column("Plannummer", nameof(Row.Number), new DataGridLength(130)));
        grid.Columns.Add(Column("Bezeichnung", nameof(Row.Title), new DataGridLength(1, DataGridLengthUnitType.Star)));
        grid.Columns.Add(Column("Index", nameof(Row.Indexes), new DataGridLength(110)));
        grid.Columns.Add(Column("Bereiche", nameof(Row.Changes), new DataGridLength(80)));
        grid.Columns.Add(Column("Ergebnis", nameof(Row.Summary), new DataGridLength(2, DataGridLengthUnitType.Star)));
        grid.MouseDoubleClick += (_, _) =>
        {
            if (grid.SelectedItem is Row row)
            {
                _openSingle(row.Result.Plan);
            }
        };

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
        _clouds = new Button { Style = (Style)FindResource("ButtonPrimary"), Content = "Änderungswolken in alle neuen Stände eintragen", IsEnabled = false };
        _clouds.Click += (_, _) => AddClouds();
        _export = new Button { Style = (Style)FindResource("ButtonSecondary"), Content = "Änderungsbericht (Excel) …", IsEnabled = false, Margin = new Thickness(8, 0, 0, 0) };
        _export.Click += (_, _) => Export();
        _cancel = new Button { Style = (Style)FindResource("ButtonSecondary"), Content = "Abbrechen", Margin = new Thickness(8, 0, 0, 0) };
        _cancel.Click += (_, _) => _stop.Cancel();
        buttons.Children.Add(_clouds);
        buttons.Children.Add(_export);
        buttons.Children.Add(_cancel);

        var dock = new DockPanel { Margin = new Thickness(24) };
        DockPanel.SetDock(head, Dock.Top);
        DockPanel.SetDock(buttons, Dock.Bottom);
        dock.Children.Add(head);
        dock.Children.Add(buttons);
        dock.Children.Add(grid);
        Content = dock;
        Loaded += async (_, _) => await RunAsync();
        Closed += (_, _) => _stop.Cancel();
    }

    /// <summary>Für den Screenshot-Modus: wartet, bis alle Vergleiche fertig sind.</summary>
    internal Task? Running { get; private set; }

    private async Task RunAsync()
    {
        if (_plans.Count == 0)
        {
            _status.Text = "Kein Plan hat mehr als einen Stand. Zum Vergleichen zuerst einen neuen Index eintragen.";
            _cancel.IsEnabled = false;
            Running = Task.CompletedTask;
            return;
        }
        var work = RunCore();
        Running = work;
        await work;
    }

    private async Task RunCore()
    {
        for (var i = 0; i < _plans.Count; i++)
        {
            if (_stop.IsCancellationRequested)
            {
                break;
            }
            var plan = _plans[i];
            _status.Text = $"{i + 1} von {_plans.Count}: {plan.Number} {plan.Title}";
            _progress.Value = (double)i / _plans.Count;
            PlanComparisonResult result;
            try
            {
                result = await Task.Run(() => PlanSetComparer.Compare(_project, plan, _stop.Token));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            _results.Add(result);
            _rows.Add(new Row(result));
        }
        _progress.Value = 1;
        var changed = _results.Count(r => r.TotalChanges > 0);
        var failed = _results.Count(r => r.Error is not null);
        _status.Text = (_results.Count < _plans.Count ? $"Abgebrochen nach {_results.Count} von {_plans.Count} Plänen. " : $"{_results.Count} Pläne verglichen. ")
            + $"{changed} mit Änderungen" + (failed > 0 ? $", {failed} mit Fehlern." : ".");
        _clouds.IsEnabled = changed > 0;
        _export.IsEnabled = _results.Count > 0;
        _cancel.IsEnabled = false;
    }

    /// <summary>Wolken um jeden geänderten Bereich in die neuen Stände; geöffnete Dateien bekommen sie mit Rückgängig.</summary>
    private void AddClouds()
    {
        var targets = _results.Where(r => r.TotalChanges > 0).ToList();
        if (MessageBox.Show(this, $"In {targets.Count} Plänen werden rote Wolken um die geänderten Bereiche eingetragen und die Dateien gespeichert. Fortfahren?",
                "Änderungswolken eintragen", MessageBoxButton.OKCancel, MessageBoxImage.Question) != MessageBoxResult.OK)
        {
            return;
        }
        var outliner = new Services.WpfTextOutliner();
        var done = 0;
        var errors = new List<string>();
        foreach (var r in targets)
        {
            var path = _project.FullPath(r.New);
            try
            {
                PdfDocument? file = null;
                try
                {
                    foreach (var page in r.Pages.Where(p => p.Changes.Count > 0))
                    {
                        var clouds = PageComparer.ChangeClouds(page.Changes, Environment.UserName, PlanSetComparer.CloudText(r));
                        if (file is null && _addToOpen(path, page.Page, clouds))
                        {
                            continue;
                        }
                        file ??= PdfDocument.Open(path);
                        file.SetMarkups(page.Page, [.. file.GetMarkups(page.Page), .. clouds], outliner);
                    }
                    file?.Save();
                }
                finally
                {
                    file?.Dispose();
                }
                done++;
            }
            catch (Exception ex) when (ex is PdfException or IOException or UnauthorizedAccessException)
            {
                errors.Add($"{r.Plan.Number}: {ex.Message}");
            }
        }
        _clouds.IsEnabled = false;
        _status.Text = $"Wolken in {done} Plänen eingetragen." + (errors.Count > 0 ? " Fehler: " + string.Join("; ", errors) : "");
    }

    private void Export()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel-Arbeitsmappe (*.xlsx)|*.xlsx",
            FileName = $"Änderungsbericht {DateTime.Now:yyyy-MM-dd}.xlsx",
            InitialDirectory = _project.Root,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }
        try
        {
            File.WriteAllBytes(dialog.FileName, PlanSetComparer.ToXlsx(_results));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Der Bericht konnte nicht gespeichert werden: {ex.Message}";
        }
    }
}
