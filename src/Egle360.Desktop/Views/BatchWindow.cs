using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Egle360.Desktop.Services;
using Egle360.Pdf.Batch;
using Egle360.Pdf.Markups;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>
/// Stapelverarbeitung: ein Arbeitsschritt (Stempel, Maßstab) für viele PDF-Dateien, mit Fortschritt, Abbruch und
/// Protokoll. Standardmäßig entstehen bearbeitete Kopien in einem Zielordner; die Originale bleiben unverändert.
/// </summary>
public sealed class BatchWindow : Window
{
    private readonly ObservableCollection<string> _files = [];
    private readonly ObservableCollection<string> _log = [];
    private readonly ListBox _fileList;
    private readonly RadioButton _stampChoice;
    private readonly RadioButton _scaleChoice;
    private readonly Button _stampButton;
    private readonly Button _cornerButton;
    private readonly TextBox _ratioBox;
    private readonly RadioButton _firstPage;
    private readonly RadioButton _copies;
    private readonly TextBox _folderBox;
    private readonly CheckBox _skipDone;
    private readonly ProgressBar _progress = new() { Height = 6, Minimum = 0, Maximum = 1, Margin = new Thickness(0, 12, 0, 0) };
    private readonly TextBlock _status;
    private readonly Button _start;
    private readonly Button _cancel;
    private readonly Button _saveLog;
    private StampPreset _stamp = StampPreset.Defaults[0];
    private PageCorner _corner = PageCorner.TopRight;
    private CancellationTokenSource? _running;
    private BatchReport? _report;
    private bool _closeRequested;

    public BatchWindow(Window owner, IEnumerable<string> files, string? defaultFolder = null)
    {
        Owner = owner;
        Title = "Stapelverarbeitung";
        Width = Math.Min(1100, owner.ActualWidth * 0.9);
        Height = Math.Min(760, owner.ActualHeight * 0.9);
        MinWidth = 760;
        MinHeight = 520;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "Surface1Brush");

        // Linke Spalte: Dateien.
        _fileList = new ListBox { Style = (Style)FindResource("PlainList"), ItemsSource = _files, SelectionMode = SelectionMode.Extended, AllowDrop = true };
        _fileList.Drop += OnDrop;
        var fileButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 0) };
        fileButtons.Children.Add(SecondaryButton("Dateien hinzufügen …", (_, _) => AddFiles()));
        fileButtons.Children.Add(SecondaryButton("Ordner hinzufügen …", (_, _) => AddFolder()));
        fileButtons.Children.Add(SecondaryButton("Entfernen", (_, _) =>
        {
            foreach (var f in _fileList.SelectedItems.Cast<string>().ToList())
            {
                _files.Remove(f);
            }
        }));
        var left = new DockPanel { Margin = new Thickness(0, 0, 24, 0) };
        var fileTitle = Text("Dateien", "TextSubtitle");
        DockPanel.SetDock(fileTitle, Dock.Top);
        left.Children.Add(fileTitle);
        var fileHint = Text("PDF-Dateien oder Ordner hierher ziehen oder über die Schaltflächen hinzufügen.", "TextCaption");
        DockPanel.SetDock(fileHint, Dock.Top);
        left.Children.Add(fileHint);
        DockPanel.SetDock(fileButtons, Dock.Bottom);
        left.Children.Add(fileButtons);
        var listBorder = new Border { Child = _fileList, BorderThickness = new Thickness(1), Margin = new Thickness(0, 8, 0, 0) };
        listBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        left.Children.Add(listBorder);

        // Rechte Spalte: Arbeitsschritt, Ziel, Start.
        var right = new StackPanel();
        right.Children.Add(Text("Arbeitsschritt", "TextSubtitle"));
        _stampChoice = Choice("Stempel setzen", "action", true);
        right.Children.Add(_stampChoice);
        var stampRow = Row();
        _stampButton = SecondaryButton(_stamp.Title, (_, _) => ShowStampMenu());
        _cornerButton = SecondaryButton(StampBatchAction.CornerText(_corner), (_, _) => ShowCornerMenu());
        stampRow.Children.Add(_stampButton);
        stampRow.Children.Add(_cornerButton);
        right.Children.Add(stampRow);
        _scaleChoice = Choice("Maßstab festlegen", "action", false);
        right.Children.Add(_scaleChoice);
        var scaleRow = Row();
        scaleRow.Children.Add(Text("1 :", "TextBody", new Thickness(0, 0, 6, 0)));
        _ratioBox = new TextBox { Style = (Style)FindResource("InputBox"), Width = 90, Text = "50" };
        scaleRow.Children.Add(_ratioBox);
        right.Children.Add(scaleRow);
        right.Children.Add(Text("Seiten", "TextSubtitle", new Thickness(0, 16, 0, 0)));
        _firstPage = Choice("Nur erste Seite", "pages", true);
        right.Children.Add(_firstPage);
        right.Children.Add(Choice("Alle Seiten", "pages", false));

        right.Children.Add(Text("Ergebnis", "TextSubtitle", new Thickness(0, 16, 0, 0)));
        _copies = Choice("Bearbeitete Kopien in diesem Ordner speichern", "target", true);
        right.Children.Add(_copies);
        var folderRow = new DockPanel { Margin = new Thickness(24, 4, 0, 4) };
        var browse = SecondaryButton("Ordner …", (_, _) => ChooseFolder());
        DockPanel.SetDock(browse, Dock.Right);
        folderRow.Children.Add(browse);
        _folderBox = new TextBox { Style = (Style)FindResource("InputBox"), Text = defaultFolder ?? "" };
        folderRow.Children.Add(_folderBox);
        right.Children.Add(folderRow);
        right.Children.Add(Choice("Originale überschreiben", "target", false));
        _skipDone = new CheckBox { Content = "Bereits erledigte Dateien überspringen (zum Fortsetzen)", IsChecked = true, Margin = new Thickness(24, 4, 0, 0) };
        _skipDone.SetResourceReference(ForegroundProperty, "TextBrush");
        right.Children.Add(_skipDone);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
        _start = new Button { Style = (Style)FindResource("ButtonPrimary"), Content = "Starten" };
        _start.Click += async (_, _) => await RunAsync();
        _cancel = SecondaryButton("Abbrechen", (_, _) => _running?.Cancel());
        _cancel.IsEnabled = false;
        _saveLog = SecondaryButton("Protokoll speichern …", (_, _) => SaveLog());
        _saveLog.IsEnabled = false;
        actions.Children.Add(_start);
        actions.Children.Add(_cancel);
        actions.Children.Add(_saveLog);
        right.Children.Add(actions);
        right.Children.Add(_progress);
        _status = Text("", "TextSecondary", new Thickness(0, 8, 0, 0));
        _status.TextWrapping = TextWrapping.Wrap;
        right.Children.Add(_status);

        var logList = new ListBox { Style = (Style)FindResource("PlainList"), ItemsSource = _log, MinHeight = 120 };
        var logBorder = new Border { Child = logList, BorderThickness = new Thickness(1), Margin = new Thickness(0, 8, 0, 0) };
        logBorder.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var grid = new Grid { Margin = new Thickness(24) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(380) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(3, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(2, GridUnitType.Star), MinHeight = 120 });
        Grid.SetColumn(right, 1);
        var rightScroll = new ScrollViewer { Content = right, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetColumn(rightScroll, 1);
        grid.Children.Add(left);
        grid.Children.Add(rightScroll);
        var logTitle = Text("Protokoll", "TextSubtitle", new Thickness(0, 16, 0, 0));
        Grid.SetRow(logTitle, 1);
        Grid.SetColumnSpan(logTitle, 2);
        grid.Children.Add(logTitle);
        Grid.SetRow(logBorder, 2);
        Grid.SetColumnSpan(logBorder, 2);
        grid.Children.Add(logBorder);
        var root = new Border { Child = grid };
        root.SetResourceReference(Border.BackgroundProperty, "Surface1Brush");
        Content = root;

        foreach (var f in files)
        {
            AddFile(f);
        }
        Closing += (_, e) =>
        {
            // Während des Laufs erst abbrechen; das Fenster schließt, sobald die aktuelle Datei fertig ist.
            if (_running is not null)
            {
                _running.Cancel();
                _closeRequested = true;
                e.Cancel = true;
                _status.Text = "Wird nach der aktuellen Datei beendet …";
            }
        };
    }

    private TextBlock Text(string text, string style, Thickness? margin = null) =>
        new() { Text = text, Style = (Style)FindResource(style), Margin = margin ?? new Thickness(0, 0, 0, 4) };

    private RadioButton Choice(string text, string group, bool isChecked) =>
        new() { Content = text, GroupName = group, IsChecked = isChecked, Style = (Style)FindResource("ChoiceRadio") };

    private static StackPanel Row() => new() { Orientation = Orientation.Horizontal, Margin = new Thickness(24, 2, 0, 6) };

    private Button SecondaryButton(string text, RoutedEventHandler click)
    {
        var b = new Button { Style = (Style)FindResource("ButtonSecondary"), Content = text, Margin = new Thickness(0, 0, 8, 0) };
        b.Click += click;
        return b;
    }

    private void AddFile(string path)
    {
        var full = Path.GetFullPath(path);
        if (Path.GetExtension(full).Equals(".pdf", StringComparison.OrdinalIgnoreCase) && !_files.Contains(full, StringComparer.OrdinalIgnoreCase))
        {
            _files.Add(full);
        }
    }

    private void AddFiles()
    {
        var dialog = new OpenFileDialog { Filter = "PDF-Dateien (*.pdf)|*.pdf", Multiselect = true };
        if (dialog.ShowDialog(this) == true)
        {
            foreach (var f in dialog.FileNames)
            {
                AddFile(f);
            }
        }
    }

    private void AddFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Ordner mit PDF-Dateien" };
        if (dialog.ShowDialog(this) == true)
        {
            AddDirectory(dialog.FolderName);
        }
    }

    /// <summary>Alle PDF-Dateien eines Ordners (ohne Unterordner), nach Namen sortiert.</summary>
    private void AddDirectory(string folder)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(folder, "*.pdf").Order(StringComparer.CurrentCultureIgnoreCase))
            {
                AddFile(f);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Der Ordner konnte nicht gelesen werden: {ex.Message}";
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                AddDirectory(p);
            }
            else
            {
                AddFile(p);
            }
        }
    }

    private void ChooseFolder()
    {
        var dialog = new OpenFolderDialog { Title = "Zielordner für die bearbeiteten Kopien" };
        if (dialog.ShowDialog(this) == true)
        {
            _folderBox.Text = dialog.FolderName;
        }
    }

    private void ShowStampMenu()
    {
        var menu = new ContextMenu { PlacementTarget = _stampButton, Placement = PlacementMode.Bottom };
        foreach (var preset in StampPreset.Defaults)
        {
            var item = new MenuItem { Header = preset.Title, IsCheckable = true, IsChecked = preset == _stamp };
            item.Click += (_, _) =>
            {
                _stamp = preset;
                _stampButton.Content = preset.Title;
                _stampChoice.IsChecked = true;
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void ShowCornerMenu()
    {
        var menu = new ContextMenu { PlacementTarget = _cornerButton, Placement = PlacementMode.Bottom };
        foreach (var corner in Enum.GetValues<PageCorner>())
        {
            var item = new MenuItem { Header = StampBatchAction.CornerText(corner), IsCheckable = true, IsChecked = corner == _corner };
            item.Click += (_, _) =>
            {
                _corner = corner;
                _cornerButton.Content = StampBatchAction.CornerText(corner);
                _stampChoice.IsChecked = true;
            };
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    /// <summary>Arbeitsschritt aus den Eingaben; null mit Hinweis in der Statuszeile, wenn etwas fehlt.</summary>
    private IBatchAction? BuildAction()
    {
        var pages = _firstPage.IsChecked == true ? BatchPages.First : BatchPages.All;
        if (_scaleChoice.IsChecked == true)
        {
            if (!Measurement.TryParseNumber(_ratioBox.Text, out var ratio) || ratio > 100000)
            {
                _status.Text = "Bitte ein Maßstabsverhältnis eingeben, zum Beispiel 50 für 1:50.";
                return null;
            }
            return new ScaleBatchAction(MeasureScale.FromRatio(ratio), pages);
        }
        return new StampBatchAction(_stamp, Environment.UserName, pages, _corner, new WpfTextOutliner());
    }

    private async Task RunAsync()
    {
        if (_files.Count == 0)
        {
            _status.Text = "Bitte zuerst PDF-Dateien hinzufügen.";
            return;
        }
        string? folder = null;
        if (_copies.IsChecked == true)
        {
            if (string.IsNullOrWhiteSpace(_folderBox.Text))
            {
                _status.Text = "Bitte einen Zielordner wählen oder „Originale überschreiben“ einstellen.";
                return;
            }
            folder = _folderBox.Text.Trim();
        }
        else if (MessageBox.Show(this, $"Die {_files.Count} Originaldateien werden direkt geändert. Fortfahren?", "Originale überschreiben",
                     MessageBoxButton.OKCancel, MessageBoxImage.Warning) != MessageBoxResult.OK)
        {
            return;
        }
        if (BuildAction() is not { } action)
        {
            return;
        }

        _running = new CancellationTokenSource();
        _start.IsEnabled = _saveLog.IsEnabled = false;
        _cancel.IsEnabled = true;
        _log.Clear();
        var files = _files.ToList();
        var progress = new Progress<BatchProgress>(p =>
        {
            _progress.Value = files.Count == 0 ? 0 : (double)p.Done / files.Count;
            _status.Text = p.Done < p.Total ? $"{p.Done + 1} von {p.Total}: {Path.GetFileName(p.Current)}" : "";
        });
        try
        {
            _report = await BatchRunner.RunAsync(files, action, new BatchOptions(folder, _skipDone.IsChecked == true), progress, _running.Token,
                async () => await Dispatcher.Yield(DispatcherPriority.Background));
            foreach (var e in _report.Entries)
            {
                _log.Add($"{BatchReport.StatusText(e.Status)}: {Path.GetFileName(e.Source)}. {e.Message}");
            }
            _status.Text = $"Fertig: {_report.Summary}.";
            _progress.Value = 1;
            if (folder is not null)
            {
                TryWriteLog(Path.Combine(folder, $"Stapelprotokoll {DateTime.Now:yyyy-MM-dd HHmm}.txt"));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text = $"Der Zielordner ist nicht beschreibbar: {ex.Message}";
        }
        finally
        {
            _running.Dispose();
            _running = null;
            _start.IsEnabled = true;
            _cancel.IsEnabled = false;
            _saveLog.IsEnabled = _report is not null;
            if (_closeRequested)
            {
                _ = Dispatcher.BeginInvoke(Close);
            }
        }
    }

    private void TryWriteLog(string path)
    {
        try
        {
            File.WriteAllText(path, _report!.ToText());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _status.Text += $" Das Protokoll konnte nicht gespeichert werden: {ex.Message}";
        }
    }

    private void SaveLog()
    {
        if (_report is null)
        {
            return;
        }
        var dialog = new SaveFileDialog { Filter = "Textdatei (*.txt)|*.txt", FileName = $"Stapelprotokoll {DateTime.Now:yyyy-MM-dd HHmm}.txt" };
        if (dialog.ShowDialog(this) == true)
        {
            TryWriteLog(dialog.FileName);
        }
    }
}
