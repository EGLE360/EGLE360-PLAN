using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Egle360.Design;

namespace Egle360.Desktop.Views;

public partial class DesignSystemView : UserControl
{
    private static readonly (string Key, string Label)[] ThemeKeys =
    {
        ("Canvas", "Arbeitsfläche"), ("Surface1", "Flächen 1"), ("Surface2", "Flächen 2"), ("Border", "Rahmen"),
        ("Text", "Text"), ("TextSecondary", "Nebentext"), ("Accent", "Akzent"), ("OnAccent", "Schrift auf Akzent"),
        ("Success", "Erfolg"), ("Warning", "Warnung"), ("Critical", "Kritisch"),
    };

    private static readonly (string Key, string Label)[] FixedKeys =
    {
        ("LogoGreen", "Logo Grün"), ("LogoBlue", "Logo Blau"), ("LogoLightGray", "Logo Hellgrau"), ("LogoWhite", "Logo Weiß"),
        ("BrandDarkBlue", "Dunkelblau"), ("BrandGreen", "Markengrün"), ("BrandAnthracite", "Anthrazit"), ("BrandBlueAccent", "Blau-Akzent"),
        ("NavBackground", "Navigation"), ("NavHover", "Navigation hervorgehoben"), ("NavSelected", "Navigation ausgewählt"),
    };

    private readonly List<(TextBlock Hex, string Key)> _hexLabels = new();

    public DesignSystemView()
    {
        InitializeComponent();
        foreach (var (key, label) in ThemeKeys)
        {
            ThemeSwatches.Children.Add(CreateSwatch(key, label));
        }
        foreach (var (key, label) in FixedKeys)
        {
            FixedSwatches.Children.Add(CreateSwatch(key, label));
        }
        Loaded += (_, _) => { App.Theme.ThemeChanged += OnThemeChanged; Refresh(); };
        Unloaded += (_, _) => App.Theme.ThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Refresh();

    private void Refresh()
    {
        ThemeInfo.Text = App.Theme.Current == ThemeKind.Dark
            ? "Aktiver Modus: Dark Mode. Umschalten unter Einstellungen."
            : "Aktiver Modus: Light Mode. Umschalten unter Einstellungen.";
        foreach (var (hex, key) in _hexLabels)
        {
            hex.Text = TryFindResource(key + "Color") is Color c ? $"#{c.R:X2}{c.G:X2}{c.B:X2}" : "fehlt";
        }
    }

    private FrameworkElement CreateSwatch(string key, string label)
    {
        var swatch = new Border
        {
            Height = 56,
            CornerRadius = new CornerRadius(6, 6, 0, 0),
            BorderThickness = new Thickness(0, 0, 0, 1),
        };
        swatch.SetResourceReference(Border.BackgroundProperty, key + "Brush");
        swatch.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");

        var name = new TextBlock { Text = label, Margin = new Thickness(10, 8, 10, 0) };
        name.SetResourceReference(StyleProperty, "TextBody");
        var hex = new TextBlock { Margin = new Thickness(10, 2, 10, 8), FontFamily = (FontFamily)FindResource("FontFamilyMono") };
        hex.SetResourceReference(StyleProperty, "TextCaption");
        _hexLabels.Add((hex, key));

        var card = new Border { Width = 168, Margin = new Thickness(0, 0, 12, 12), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6) };
        card.SetResourceReference(Border.BackgroundProperty, "Surface1Brush");
        card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        card.Child = new StackPanel { Children = { swatch, name, hex } };
        return card;
    }

    private void OnSampleClick(object sender, RoutedEventArgs e) =>
        ClickInfo.Text = $"„{((Button)sender).Content}“ ausgelöst um {DateTime.Now:HH:mm:ss}.";
}
