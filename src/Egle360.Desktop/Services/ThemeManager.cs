using System.Windows;
using Egle360.Design;
using Microsoft.Win32;

namespace Egle360.Desktop.Services;

/// <summary>Schaltet zwischen Light und Dark Mode um und speichert die Wahl.</summary>
public sealed class ThemeManager
{
    private const int ThemeDictionaryIndex = 1;
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    private readonly Application _app;
    private readonly UiSettingsStore _store;

    public ThemeManager(Application app, UiSettingsStore store)
    {
        _app = app;
        _store = store;
        Settings = store.Load();
    }

    public event EventHandler? ThemeChanged;

    public UiSettings Settings { get; }

    public ThemeKind Current { get; private set; }

    public void Initialize()
    {
        Apply();
        SystemEvents.UserPreferenceChanged += (_, e) =>
        {
            if (e.Category == UserPreferenceCategory.General && Settings.Theme == ThemePreference.System)
            {
                _app.Dispatcher.Invoke(Apply);
            }
        };
    }

    public void SetPreference(ThemePreference preference)
    {
        Settings.Theme = preference;
        _store.Save(Settings);
        Apply();
    }

    public void SaveSettings() => _store.Save(Settings);

    private void Apply()
    {
        var kind = ThemeResolver.Resolve(Settings.Theme, SystemUsesLightTheme());
        var source = new Uri($"Themes/Colors.{kind}.xaml", UriKind.Relative);
        _app.Resources.MergedDictionaries[ThemeDictionaryIndex] = new ResourceDictionary { Source = source };
        Current = kind;
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static bool SystemUsesLightTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
        return key?.GetValue("AppsUseLightTheme") is not int value || value != 0;
    }
}
