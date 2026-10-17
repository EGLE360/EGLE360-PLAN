using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Runtime.CompilerServices;
using System.Windows.Media;

namespace Egle360.Desktop.Controls;

/// <summary>Hilfen für das Menüband: Schaltflächen mit Symbol und Beschriftung, Gruppen mit Überschrift.</summary>
public static class Ribbon
{
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    private const string LabelTag = "RibbonLabel";

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
        stack.Children.Add(new TextBlock { Text = label, HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center, Tag = LabelTag });
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

    /// <summary>
    /// Passt das Menüband an die Breite an: Reicht der Platz nicht für Symbol und Beschriftung, werden nur Symbole
    /// gezeigt (die Beschriftung steht dann im Hinweistext). Grundlage ist die tatsächliche Breite nach dem Layout.
    /// </summary>
    public static void FitOnResize(ScrollViewer scroll)
    {
        scroll.ScrollChanged += (_, e) =>
        {
            if (e.ExtentWidthChange != 0 || e.ViewportWidthChange != 0)
            {
                Decide(scroll);
            }
        };
    }

    /// <summary>Breite, die das Menüband mit Beschriftungen braucht, je Bildlaufbereich (0 = noch nicht bekannt).</summary>
    private static readonly ConditionalWeakTable<ScrollViewer, StrongBox<double>> NeededWidth = new();

    /// <summary>Nach einer Änderung des Inhalts (etwa einer anderen Registerkarte) neu prüfen.</summary>
    public static void Fit(ScrollViewer scroll)
    {
        if (scroll.Content is not FrameworkElement content)
        {
            return;
        }
        NeededWidth.GetOrCreateValue(scroll).Value = 0;
        SetCompact(content, false);
    }

    private static void Decide(ScrollViewer scroll)
    {
        if (scroll.Content is not FrameworkElement content || scroll.ViewportWidth <= 0)
        {
            return;
        }
        var needed = NeededWidth.GetOrCreateValue(scroll);
        if (needed.Value <= 0)
        {
            // Ausgeklappt gemessen: Das ist die Breite mit Beschriftungen.
            needed.Value = scroll.ExtentWidth;
        }
        SetCompact(content, needed.Value > scroll.ViewportWidth + 1);
    }

    private static void SetCompact(DependencyObject root, bool compact)
    {
        foreach (var button in Descendants<ButtonBase>(root))
        {
            if (button.Content is not StackPanel { Children.Count: > 0 } stack || stack.Children[^1] is not TextBlock { Tag: LabelTag } label)
            {
                continue;
            }
            var visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            if (label.Visibility == visibility)
            {
                continue;
            }
            label.Visibility = visibility;
            if (compact)
            {
                button.Height = 34;
                button.MinWidth = 34;
            }
            else
            {
                button.ClearValue(FrameworkElement.HeightProperty);
                button.ClearValue(FrameworkElement.MinWidthProperty);
            }
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
