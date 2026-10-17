using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Egle360.Pdf;
using Egle360.Pdf.Markups;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>Werkzeugbibliothek in der Seitenleiste: gespeicherte Werkzeuge wählen, anlegen, ordnen und austauschen.</summary>
public partial class DocumentsView
{
    private const string ToolFilter = "EGLE360-Werkzeugsätze (*.e360tools.json)|*.e360tools.json|JSON-Dateien (*.json)|*.json";

    /// <summary>Ein Eintrag der Werkzeugliste.</summary>
    private sealed record ToolItem(string SetName, ToolPreset Tool)
    {
        public string Name => Tool.Name;

        public string Summary => Tool.Summary;

        public Brush StrokeBrush => Frozen(Tool.Style.Stroke);

        public Brush FillBrush => Tool.Style.Fill is { } f ? Frozen(f) : Brushes.Transparent;

        public Visibility FavoriteVisibility => Tool.Favorite ? Visibility.Visible : Visibility.Collapsed;

        private static Brush Frozen(PdfColor c)
        {
            var b = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            b.Freeze();
            return b;
        }
    }

    private bool _syncingToolList;

    private void InitToolLibrary()
    {
        App.Tools.Changed += (_, _) => RefreshToolList();
        RefreshToolList();
    }

    private void RefreshToolList()
    {
        var query = ToolSearchBox.Text;
        var items = App.Tools.Library.Search(query).Select(x => new ToolItem(x.Set.Name, x.Tool)).ToList();
        var view = new ListCollectionView(items);
        // Bei einer Suche stehen Favoriten oben; ohne Suche bleibt die Reihenfolge der Sätze erhalten.
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(ToolItem.SetName)));
        if (query.Trim().Length == 0)
        {
            var order = App.Tools.Library.Sets.Select((s, i) => (s.Name, i)).ToDictionary(x => x.Name, x => x.i);
            view.CustomSort = Comparer<object>.Create((a, b) =>
            {
                var x = (ToolItem)a;
                var y = (ToolItem)b;
                var c = order[x.SetName].CompareTo(order[y.SetName]);
                return c != 0 ? c : IndexIn(x).CompareTo(IndexIn(y));
            });
        }
        _syncingToolList = true;
        ToolList.ItemsSource = view;
        _syncingToolList = false;
        SyncToolList();
    }

    private static int IndexIn(ToolItem item) =>
        App.Tools.Library.Sets.First(s => s.Name == item.SetName).Tools.ToList().FindIndex(t => t.Id == item.Tool.Id);

    /// <summary>Markiert das aktive Werkzeug der Ansicht in der Liste, oder nichts.</summary>
    private void SyncToolList()
    {
        if (ToolList.ItemsSource is not ICollectionView view)
        {
            return;
        }
        var id = _active?.Viewer.ActivePreset?.Id;
        _syncingToolList = true;
        ToolList.SelectedItem = id is null ? null : view.Cast<ToolItem>().FirstOrDefault(t => t.Tool.Id == id);
        _syncingToolList = false;
    }

    private void OnToolSearch(object sender, TextChangedEventArgs e) => RefreshToolList();

    private void OnToolSelected(object sender, SelectionChangedEventArgs e)
    {
        // Rechtsklick wählt den Eintrag nur für das Kontextmenü, nicht zum Zeichnen.
        if (_syncingToolList || ToolList.SelectedItem is not ToolItem item || Mouse.RightButton == MouseButtonState.Pressed)
        {
            return;
        }
        if (_active is not { Document.CanAnnotate: true } open)
        {
            SyncToolList();
            return;
        }
        open.Viewer.StartPreset(item.Tool);
        open.Viewer.FocusView();
    }

    private ToolItem? MenuTool => _toolMenuItem ?? ToolList.SelectedItem as ToolItem;

    /// <summary>Eintrag unter der Maus beim Öffnen des Kontextmenüs (Rechtsklick wählt nicht aus).</summary>
    private ToolItem? _toolMenuItem;

    private void OnToolMenuOpening(object sender, ContextMenuEventArgs e)
    {
        _toolMenuItem = (e.OriginalSource as FrameworkElement)?.DataContext as ToolItem;
        var item = _toolMenuItem ?? ToolList.SelectedItem as ToolItem;
        ToolFavoriteItem.Header = item?.Tool.Favorite == true ? "Favorit entfernen" : "Als Favorit markieren";
        ToolFavoriteItem.IsEnabled = ToolRenameItem.IsEnabled = ToolDeleteItem.IsEnabled = item is not null;
        SetRenameItem.IsEnabled = SetDeleteItem.IsEnabled = item is not null;
        SetRenameItem.Header = item is null ? "Satz umbenennen …" : $"Satz „{item.SetName}“ umbenennen …";
        SetDeleteItem.Header = item is null ? "Satz löschen" : $"Satz „{item.SetName}“ löschen";
    }

    private void UpdateTools(ToolLibrary library)
    {
        try
        {
            App.Tools.Update(library);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError($"Die Werkzeugbibliothek konnte nicht gespeichert werden: {ex.Message}");
        }
    }

    private void OnToolFavorite(object sender, RoutedEventArgs e)
    {
        if (MenuTool is { } item)
        {
            UpdateTools(App.Tools.Library.Replace(item.Tool with { Favorite = !item.Tool.Favorite }));
        }
    }

    private void OnToolRename(object sender, RoutedEventArgs e)
    {
        if (MenuTool is { } item && Dialogs.AskText(Owner, "Werkzeug umbenennen", "Neuer Name des Werkzeugs:", item.Name) is { } name
            && name.Trim().Length > 0)
        {
            UpdateTools(App.Tools.Library.Replace(item.Tool with { Name = name.Trim() }));
        }
    }

    private void OnToolDelete(object sender, RoutedEventArgs e)
    {
        if (MenuTool is { } item)
        {
            if (_active?.Viewer.ActivePreset?.Id == item.Tool.Id)
            {
                _active.Viewer.SetTool(Controls.ViewerTool.Select);
            }
            UpdateTools(App.Tools.Library.Remove(item.Tool.Id));
        }
    }

    private void OnToolSetRename(object sender, RoutedEventArgs e)
    {
        if (MenuTool is not { } item
            || Dialogs.AskText(Owner, "Werkzeugsatz umbenennen", "Neuer Name des Satzes:", item.SetName) is not { } name
            || (name = name.Trim()).Length == 0 || name == item.SetName)
        {
            return;
        }
        if (App.Tools.Library.Sets.Any(s => s.Name == name))
        {
            ShowError($"Es gibt schon einen Werkzeugsatz „{name}“.");
            return;
        }
        UpdateTools(App.Tools.Library.RenameSet(item.SetName, name));
    }

    private void OnToolSetDelete(object sender, RoutedEventArgs e)
    {
        if (MenuTool is not { } item)
        {
            return;
        }
        var count = App.Tools.Library.Sets.First(s => s.Name == item.SetName).Tools.Count;
        if (MessageBox.Show(Owner, $"Den Werkzeugsatz „{item.SetName}“ mit {count} Werkzeug(en) löschen?", "EGLE360 PLAN",
                MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            _active?.Viewer.SetTool(Controls.ViewerTool.Select);
            UpdateTools(App.Tools.Library.RemoveSet(item.SetName));
        }
    }

    private void OnToolsImport(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = ToolFilter, Title = "Werkzeugsätze importieren" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        try
        {
            var imported = ToolLibrary.FromJson(File.ReadAllText(dialog.FileName));
            var before = App.Tools.Library.AllTools.Count();
            var merged = App.Tools.Library.Merge(imported);
            UpdateTools(merged);
            var added = merged.AllTools.Count() - before;
            MessageBox.Show(Owner, added == 0 ? "Alle Werkzeuge der Datei waren schon vorhanden." : $"{added} Werkzeug(e) übernommen.",
                "EGLE360 PLAN", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
    }

    private void OnToolsExport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = ToolFilter, FileName = "Werkzeuge.e360tools.json", Title = "Werkzeugsätze exportieren" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        try
        {
            File.WriteAllText(dialog.FileName, App.Tools.Library.ToJson());
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
    }

    private void OnToolsDefaults(object sender, RoutedEventArgs e) => UpdateTools(App.Tools.Library.Merge(ToolLibrary.Defaults()));

    /// <summary>Speichert eine Markierung als neues Werkzeug mit ihrer Darstellung, ihrem Text, ihrer Höhe und ihrem Preis.</summary>
    private void OnSaveAsTool(Markup markup)
    {
        var suggestion = markup.Text.Length is > 0 and <= 40 && !markup.Text.Contains('\n') ? markup.Text : MarkupKinds.DisplayName(markup.Kind);
        if (Dialogs.AskText(Owner, "Als Werkzeug speichern", "Name des Werkzeugs:", suggestion) is not { } name || (name = name.Trim()).Length == 0)
        {
            return;
        }
        var defaultSet = App.Tools.Library.Sets.LastOrDefault()?.Name ?? "Eigene Werkzeuge";
        if (Dialogs.AskText(Owner, "Als Werkzeug speichern",
                $"Werkzeugsatz (vorhanden: {string.Join(", ", App.Tools.Library.Sets.Select(s => s.Name).DefaultIfEmpty("keiner"))}). Ein neuer Name legt einen neuen Satz an.",
                defaultSet) is not { } set || (set = set.Trim()).Length == 0)
        {
            return;
        }
        UpdateTools(App.Tools.Library.Add(set, ToolPreset.FromMarkup(markup, name)));
        ToolsTab.IsChecked = true;
    }

    /// <summary>Für den Screenshot-Modus: Werkzeugbibliothek zeigen und ein Werkzeug wählen.</summary>
    internal void ShowToolsForScreenshot(string toolName)
    {
        ToolsTab.IsChecked = true;
        if (App.Tools.Library.AllTools.FirstOrDefault(t => t.Name == toolName) is { } tool)
        {
            _active?.Viewer.StartPreset(tool);
        }
    }
}
