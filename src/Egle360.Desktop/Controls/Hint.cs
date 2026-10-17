using System.Windows;

namespace Egle360.Desktop.Controls;

/// <summary>Hinweistext, den ein leeres Eingabefeld grau anzeigt (zum Beispiel „Pläne suchen …“).</summary>
public static class Hint
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.RegisterAttached("Text", typeof(string), typeof(Hint), new PropertyMetadata(null));

    public static string? GetText(DependencyObject element) => (string?)element.GetValue(TextProperty);

    public static void SetText(DependencyObject element, string? value) => element.SetValue(TextProperty, value);
}
