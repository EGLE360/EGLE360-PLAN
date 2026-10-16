using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Egle360.Pdf;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Views;

/// <summary>Kleine Dialoge im EGLE360-Design (Kennwort, Texteingabe, Dokumenteigenschaften).</summary>
internal static class Dialogs
{
    public static string? AskPassword(Window owner, string fileName, bool retry)
    {
        var box = new PasswordBox { Style = (Style)owner.FindResource("InputPassword"), Margin = new Thickness(0, 12, 0, 0) };
        var hint = retry
            ? $"Das Kennwort für „{fileName}“ war nicht richtig. Bitte erneut eingeben."
            : $"„{fileName}“ ist durch ein Kennwort geschützt.";
        return Show(owner, "Kennwort erforderlich", hint, box, "Öffnen", () => box.Password, () => box.Focus());
    }

    public static string? AskText(Window owner, string title, string hint, string initial)
    {
        var box = new TextBox { Style = (Style)owner.FindResource("InputBox"), Text = initial, Margin = new Thickness(0, 12, 0, 0) };
        return Show(owner, title, hint, box, "Weiter", () => box.Text, () => { box.Focus(); box.SelectAll(); });
    }

    /// <summary>Mehrzeilige Texteingabe (Markierungstext, Kommentar). Strg+Eingabe übernimmt.</summary>
    public static string? AskMultilineText(Window owner, string title, string hint, string initial)
    {
        var box = new TextBox
        {
            Style = (Style)owner.FindResource("InputBox"),
            Text = initial,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MinHeight = 110,
            MaxHeight = 320,
            Width = 460,
            VerticalContentAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 12, 0, 0),
        };
        return Show(owner, title, hint + " Übernehmen mit Strg+Eingabe.", box, "Übernehmen", () => box.Text, () => { box.Focus(); box.CaretIndex = box.Text.Length; },
            submitOnCtrlEnter: box);
    }

    /// <summary>Wirkliches Maß der gezogenen Kalibrierstrecke mit Einheit; optional für alle Seiten.</summary>
    public static (double Value, MeasureUnit Unit, bool AllPages)? AskCalibration(Window owner, MeasureUnit unit, bool offerAllPages)
    {
        var box = new TextBox { Style = (Style)owner.FindResource("InputBox"), Width = 140 };
        var units = new ComboBox { ItemsSource = Enum.GetValues<MeasureUnit>().Select(Measurement.Symbol).ToList(), SelectedIndex = (int)unit, Width = 70, Margin = new Thickness(8, 0, 0, 0) };
        var all = new CheckBox { Content = "Für alle Seiten übernehmen", Margin = new Thickness(0, 12, 0, 0), Visibility = offerAllPages ? Visibility.Visible : Visibility.Collapsed };
        all.SetResourceReference(Control.ForegroundProperty, "TextBrush");
        var error = new TextBlock { Style = (Style)owner.FindResource("TextCaption"), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, Text = "Bitte eine Zahl größer als null eingeben, zum Beispiel 4,25." };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(box);
        row.Children.Add(units);
        var content = new StackPanel();
        content.Children.Add(row);
        content.Children.Add(error);
        content.Children.Add(all);
        double value = 0;
        var answer = Show(owner, "Maßstab kalibrieren", "Wie lang ist die gezogene Strecke in Wirklichkeit?", content, "Übernehmen",
            () => Measurement.TryParseNumber(box.Text, out value) ? "ok" : "", () => box.Focus(),
            validate: () =>
            {
                var ok = Measurement.TryParseNumber(box.Text, out _);
                error.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
                return ok;
            });
        return answer == "ok" ? (value, (MeasureUnit)units.SelectedIndex, all.IsChecked == true) : null;
    }

    /// <summary>
    /// Höhe (Wandfläche) oder Tiefe (Volumen) in Metern. Liefert null bei Abbruch, sonst den Wert; ein leeres Feld
    /// ergibt 0 und entfernt die Angabe.
    /// </summary>
    public static double? AskDepth(Window owner, string title, string hint, double? current) =>
        AskPositiveNumber(owner, title, hint, current, "m", "2,80");

    /// <summary>Zahl größer null mit Einheit. Liefert null bei Abbruch, 0 bei leerem Feld (Angabe entfernen).</summary>
    public static double? AskPositiveNumber(Window owner, string title, string hint, double? current, string unitText, string example)
    {
        var box = new TextBox { Style = (Style)owner.FindResource("InputBox"), Width = 140, Text = current is { } c ? Measurement.Number(c, 2) : "" };
        var unit = new TextBlock { Style = (Style)owner.FindResource("TextBody"), Text = unitText, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
        var error = new TextBlock { Style = (Style)owner.FindResource("TextCaption"), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, Text = $"Bitte eine Zahl größer als null eingeben, zum Beispiel {example}, oder das Feld leeren." };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
        row.Children.Add(box);
        row.Children.Add(unit);
        var content = new StackPanel();
        content.Children.Add(row);
        content.Children.Add(error);
        bool Valid(out double value)
        {
            value = 0;
            return box.Text.Trim().Length == 0 || (Measurement.TryParseNumber(box.Text, out value) && value > 0 && value < 1e6);
        }
        double result = 0;
        var answer = Show(owner, title, hint + " Leer lassen, um die Angabe zu entfernen.", content, "Übernehmen",
            () => Valid(out result) ? "ok" : "", () => { box.Focus(); box.SelectAll(); },
            validate: () =>
            {
                var ok = Valid(out _);
                error.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
                return ok;
            });
        return answer == "ok" ? result : null;
    }

    public static void ShowProperties(Window owner, PdfDocument doc, string title)
    {
        var meta = doc.GetMetadata();
        var pages = doc.GetPages();
        var formats = pages.GroupBy(p => p.Format.Description)
            .Select(g => g.Count() == 1 ? g.Key : $"{g.Count()} × {g.Key}");
        var rows = new List<(string, string?)>
        {
            ("Datei", doc.FilePath ?? "Noch nicht gespeichert"),
            ("Dateigröße", doc.FilePath is { } p && File.Exists(p) ? FormatSize(new FileInfo(p).Length) : null),
            ("PDF-Version", doc.FileVersion is { } v ? $"{v / 10}.{v % 10}" : null),
            ("Seiten", pages.Count.ToString(CultureInfo.CurrentCulture)),
            ("Formate", string.Join("\n", formats)),
            ("Titel", meta.Title),
            ("Autor", meta.Author),
            ("Thema", meta.Subject),
            ("Stichwörter", meta.Keywords),
            ("Erstellt mit", meta.Creator),
            ("PDF-Erzeuger", meta.Producer),
            ("Erstellt", meta.Created?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)),
            ("Geändert", meta.Modified?.ToLocalTime().ToString("g", CultureInfo.CurrentCulture)),
            ("Verschlüsselt", doc.IsEncrypted ? "ja" : "nein"),
            ("Änderungen erlaubt", doc.CanModify ? "ja" : "nein, laut Berechtigungen des Dokuments"),
        };

        var grid = new Grid { Margin = new Thickness(0, 12, 0, 0) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        foreach (var (label, value) in rows)
        {
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var l = new TextBlock { Text = label, Style = (Style)owner.FindResource("TextSecondary"), Margin = new Thickness(0, 3, 12, 3) };
            var t = new TextBox
            {
                Text = string.IsNullOrWhiteSpace(value) ? "nicht angegeben" : value,
                IsReadOnly = true,
                BorderThickness = new Thickness(0),
                Background = System.Windows.Media.Brushes.Transparent,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 3, 0, 3),
            };
            t.SetResourceReference(Control.ForegroundProperty, string.IsNullOrWhiteSpace(value) ? "TextSecondaryBrush" : "TextBrush");
            Grid.SetRow(l, grid.RowDefinitions.Count - 1);
            Grid.SetRow(t, grid.RowDefinitions.Count - 1);
            Grid.SetColumn(t, 1);
            grid.Children.Add(l);
            grid.Children.Add(t);
        }
        Show(owner, $"Eigenschaften von {title}", "Angaben aus der Datei. Ändern der Metadaten folgt in einer späteren Ausbaustufe.", grid, null, () => "", () => { });
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} Byte",
        < 1024 * 1024 => $"{bytes / 1024.0:0.0} KB",
        _ => $"{bytes / 1024.0 / 1024.0:0.0} MB",
    };

    /// <summary>Allgemeiner Dialog mit Titel, Hinweis, Inhalt und Schaltflächen. Liefert null bei Abbruch.</summary>
    private static string? Show(Window owner, string title, string hint, UIElement content, string? okText, Func<string> result, Action focus, UIElement? submitOnCtrlEnter = null, Func<bool>? validate = null)
    {
        var window = new Window
        {
            Title = title,
            Owner = owner,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 420,
            MaxWidth = 720,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
        };
        window.SetResourceReference(Window.BackgroundProperty, "Surface1Brush");

        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = title, Style = (Style)owner.FindResource("TextSubtitle") });
        panel.Children.Add(new TextBlock { Text = hint, Style = (Style)owner.FindResource("TextSecondary"), Margin = new Thickness(0, 6, 0, 0), MaxWidth = 560 });
        panel.Children.Add(content);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 20, 0, 0) };
        string? answer = null;
        if (okText is not null)
        {
            var ok = new Button { Content = okText, Style = (Style)owner.FindResource("ButtonPrimary"), IsDefault = true };
            ok.Click += (_, _) =>
            {
                if (validate is not null && !validate())
                {
                    return;
                }
                answer = result();
                window.DialogResult = true;
            };
            buttons.Children.Add(ok);
        }
        var cancel = new Button
        {
            Content = okText is null ? "Schließen" : "Abbrechen",
            Style = (Style)owner.FindResource("ButtonSecondary"),
            IsCancel = true,
            Margin = new Thickness(8, 0, 0, 0),
        };
        buttons.Children.Add(cancel);
        panel.Children.Add(buttons);
        window.Content = panel;
        window.Loaded += (_, _) => focus();
        if (submitOnCtrlEnter is not null)
        {
            submitOnCtrlEnter.PreviewKeyDown += (_, e) =>
            {
                if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
                {
                    answer = result();
                    window.DialogResult = true;
                    e.Handled = true;
                }
            };
        }
        return window.ShowDialog() == true ? answer : null;
    }
}

/// <summary>Zeigt aus einem Pfad nur den Dateinamen.</summary>
public sealed class FileNameConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string s ? Path.GetFileName(s) : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Eintrag in der Miniaturliste.</summary>
public sealed class ThumbnailItem : System.ComponentModel.INotifyPropertyChanged
{
    private System.Windows.Media.ImageSource? _image;

    public ThumbnailItem(int index, string label, double width, double height)
    {
        Index = index;
        Label = label;
        Width = width;
        Height = height;
    }

    public int Index { get; }

    public string Label { get; }

    public double Width { get; }

    public double Height { get; }

    public bool Requested { get; set; }

    public System.Windows.Media.ImageSource? Image
    {
        get => _image;
        set
        {
            _image = value;
            PropertyChanged?.Invoke(this, new(nameof(Image)));
        }
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

internal static class KeyGestures
{
    public static bool Ctrl(KeyEventArgs e, Key key, bool shift = false) =>
        e.Key == key && Keyboard.Modifiers == (shift ? ModifierKeys.Control | ModifierKeys.Shift : ModifierKeys.Control);
}
