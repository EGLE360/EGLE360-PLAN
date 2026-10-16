using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Egle360.Desktop.Views;
using Egle360.Design;

namespace Egle360.Desktop;

public partial class MainWindow : Window
{
    private const double ExpandedWidth = 232;
    private const double CollapsedWidth = 56;

    private readonly NavigationModel _navigation = new();
    private readonly Dictionary<string, Func<UserControl>> _pages = new();
    private readonly Dictionary<string, RadioButton> _buttons = new();

    public MainWindow()
    {
        InitializeComponent();

        // Nur funktionsfähige Module werden registriert. Weitere Module folgen in den nächsten Phasen.
        // Die Dokumentansicht bleibt beim Wechseln erhalten, damit geöffnete Dokumente offen bleiben.
        Register(new NavigationEntry("documents", "Dokumente", "\uE8A5"), () => Documents);
        Register(new NavigationEntry("design", "Designsystem", ""), () => new DesignSystemView());
        Register(new NavigationEntry("settings", "Einstellungen", "", NavigationGroup.Footer), () => new SettingsView());

        _navigation.SelectionChanged += (_, entry) =>
        {
            PageHost.Content = _pages[entry.Id]();
            _buttons[entry.Id].IsChecked = true;
        };
        _navigation.Select("documents");
        Closing += (_, e) =>
        {
            if (_documents is not null && !_documents.ConfirmCloseAll())
            {
                e.Cancel = true;
            }
        };
        Closed += (_, _) => _documents?.Dispose();

        ApplyCollapsed(App.Theme.Settings.NavigationCollapsed);
        PreviewKeyDown += OnPreviewKeyDown;
    }

    private DocumentsView? _documents;

    /// <summary>Die Dokumentansicht wird beim ersten Aufruf angelegt und danach wiederverwendet.</summary>
    public DocumentsView Documents => _documents ??= new DocumentsView();

    /// <summary>Öffnet PDF-Dateien, z. B. aus der Befehlszeile, und zeigt die Dokumentansicht.</summary>
    public void OpenDocuments(IEnumerable<string> paths)
    {
        var list = paths.ToList();
        if (list.Count == 0)
        {
            return;
        }
        ShowPage("documents");
        Documents.OpenFiles(list);
    }

    public IEnumerable<string> PageIds => _navigation.Entries.Select(e => e.Id);

    public void ShowPage(string id) => _navigation.Select(id);

    public DependencyObject? PageContent => PageHost.Content as DependencyObject;

    private void Register(NavigationEntry entry, Func<UserControl> factory)
    {
        _navigation.Register(entry);
        _pages[entry.Id] = factory;
        var button = new RadioButton
        {
            DataContext = entry,
            GroupName = "MainNavigation",
            Style = (Style)FindResource("NavItem"),
            ToolTip = entry.Title,
        };
        AutomationPropertiesHelper.SetName(button, entry.Title);
        button.Checked += (_, _) => _navigation.Select(entry.Id);
        _buttons[entry.Id] = button;
        (entry.Group == NavigationGroup.Main ? MainItems : FooterItems).Children.Add(button);
    }

    private void OnToggleCollapse(object sender, RoutedEventArgs e) => ToggleCollapse();

    private void ToggleCollapse()
    {
        var collapsed = !App.Theme.Settings.NavigationCollapsed;
        App.Theme.Settings.NavigationCollapsed = collapsed;
        App.Theme.SaveSettings();
        ApplyCollapsed(collapsed);
    }

    private void ApplyCollapsed(bool collapsed)
    {
        NavColumn.Width = new GridLength(collapsed ? CollapsedWidth : ExpandedWidth);
        Logo.Height = collapsed ? 22 : 56;
        Logo.Margin = collapsed ? new Thickness(10, 20, 10, 16) : new Thickness(20, 20, 20, 16);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.None)
        {
            ToggleCollapse();
            e.Handled = true;
        }
    }
}

internal static class AutomationPropertiesHelper
{
    public static void SetName(DependencyObject element, string name) =>
        System.Windows.Automation.AutomationProperties.SetName(element, name);
}
