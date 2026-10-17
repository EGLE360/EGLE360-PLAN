using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace Egle360.Desktop.Controls;

/// <summary>Hilfen für das Menüband: Schaltflächen mit Symbol und Beschriftung, Gruppen mit Überschrift.</summary>
public static class Ribbon
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    /// <summary>Macht aus einer Symbolschaltfläche eine Menüband-Schaltfläche mit Beschriftung unter dem Symbol.</summary>
    public static void Apply(ButtonBase button, string label)
    {
        var icon = button.Content switch
        {
            string glyph => new TextBlock { Text = glyph },
            UIElement element => element,
            _ => null,
        };
        button.Content = null;
        if (icon is TextBlock text)
        {
            text.FontFamily = IconFont;
            text.FontSize = 16;
        }
        var stack = new StackPanel();
        if (icon is FrameworkElement fe)
        {
            fe.HorizontalAlignment = HorizontalAlignment.Center;
            fe.Margin = new Thickness(0, 2, 0, 3);
            stack.Children.Add(fe);
        }
        stack.Children.Add(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center });
        button.Content = stack;
        button.Style = (Style)Application.Current.FindResource(button is ToggleButton ? "RibbonToggle" : "RibbonButton");
    }

    /// <summary>Alle Symbolschaltflächen unterhalb von <paramref name="root"/> umstellen; Beschriftung ist der Automationsname.</summary>
    public static void ApplyAll(DependencyObject root)
    {
        var toolButton = Application.Current.FindResource("ToolButton");
        foreach (var button in Descendants<Button>(root).Where(b => b.Style == toolButton).ToList())
        {
            Apply(button, System.Windows.Automation.AutomationProperties.GetName(button));
        }
    }

    public static HeaderedContentControl Group(string caption, UIElement content) =>
        new() { Style = (Style)Application.Current.FindResource("RibbonGroup"), Header = caption, Content = content };

    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (child is T match)
            {
                yield return match;
            }
            foreach (var nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }
}
