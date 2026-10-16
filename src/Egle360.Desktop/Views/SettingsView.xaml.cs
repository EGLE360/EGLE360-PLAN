using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Egle360.Design;

namespace Egle360.Desktop.Views;

public partial class SettingsView : UserControl
{
    private bool _initializing = true;

    public SettingsView()
    {
        InitializeComponent();
        var option = App.Theme.Settings.Theme switch
        {
            ThemePreference.Light => LightOption,
            ThemePreference.Dark => DarkOption,
            _ => SystemOption,
        };
        option.IsChecked = true;
        VersionInfo.Text = $"EGLE360 PLAN {Assembly.GetExecutingAssembly().GetName().Version?.ToString(3)}";
        _initializing = false;
    }

    private void OnThemeChecked(object sender, RoutedEventArgs e)
    {
        if (_initializing)
        {
            return;
        }
        var preference = sender == LightOption ? ThemePreference.Light
            : sender == DarkOption ? ThemePreference.Dark
            : ThemePreference.System;
        App.Theme.SetPreference(preference);
    }
}
