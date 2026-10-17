using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Egle360.Pdf;
using Egle360.Pdf.Compare;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Views;

/// <summary>
/// Vergleich zweier Planstände: farbige Überlagerung Seite für Seite und auf Wunsch Wolken um die Änderungen im
/// neuen Stand.
/// </summary>
public sealed class CompareWindow : Window
{
    private readonly PdfDocument _old;
    private readonly PdfDocument _new;
    private readonly string _newPath;
    private readonly string _oldLabel;
    private readonly Func<string, int, IReadOnlyList<Markup>, bool> _addToOpen;
    private readonly Image _image = new() { Stretch = Stretch.None, SnapsToDevicePixels = true };
    private readonly ScaleTransform _zoom = new(1, 1);
    private readonly ScrollViewer _scroll = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    private readonly TextBlock _status = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(12, 0, 12, 0) };
    private readonly TextBlock _pageText = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(6, 0, 6, 0) };
    private readonly Button _cloudButton;
    private readonly Button _prev;
    private readonly Button _next;
    private readonly CheckBox _showOld;
    private readonly CheckBox _showNew;
    private readonly CheckBox _showSame;
    private int _page;
    private PageComparison? _result;

    /// <param name="addToOpen">Trägt Wolken in ein schon geöffnetes Dokument ein (mit Rückgängig); false, wenn es nicht geöffnet ist.</param>
    public CompareWindow(Window owner, string oldPath, string oldLabel, string newPath, string newLabel, Func<string, int, IReadOnlyList<Markup>, bool> addToOpen)
    {
        Owner = owner;
        Title = $"Vergleich: {oldLabel} und {newLabel}";
        Width = Math.Min(1400, owner.ActualWidth * 0.9);
        Height = Math.Min(950, owner.ActualHeight * 0.9);
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        SetResourceReference(BackgroundProperty, "CanvasBrush");
        _old = PdfDocument.Open(oldPath);
        try
        {
            _new = PdfDocument.Open(newPath);
        }
        catch
        {
            _old.Dispose();
            throw;
        }
        _newPath = newPath;
        _oldLabel = oldLabel;
        _addToOpen = addToOpen;
        Closed += (_, _) =>
        {
            _old.Dispose();
            _new.Dispose();
        };

        var bar = new WrapPanel { VerticalAlignment = VerticalAlignment.Center };
        Button Tool(string glyph, string tip, RoutedEventHandler click)
        {
            var b = new Button { Style = (Style)FindResource("ToolButton"), Content = glyph, ToolTip = tip };
            AutomationPropertiesHelper.SetName(b, tip);
            b.Click += click;
            bar.Children.Add(b);
            return b;
        }
        _prev = Tool("", "Vorherige Seite", (_, _) => ShowPage(_page - 1));
        bar.Children.Add(_pageText);
        _next = Tool("", "Nächste Seite", (_, _) => ShowPage(_page + 1));
        bar.Children.Add(new Border { Style = (Style)FindResource("ToolSeparator") });
        Tool("", "Verkleinern", (_, _) => SetZoom(_zoom.ScaleX / 1.25));
        Tool("", "Vergrößern", (_, _) => SetZoom(_zoom.ScaleX * 1.25));
        Tool("", "Ganze Seite", (_, _) => FitPage());
        bar.Children.Add(new Border { Style = (Style)FindResource("ToolSeparator") });
        _showOld = Legend(PageComparer.OldOnly, $"nur in {oldLabel}");
        _showNew = Legend(PageComparer.NewOnly, $"nur in {newLabel}");
        _showSame = Legend(PageComparer.Same, "unverändert");
        bar.Children.Add(_showOld);
        bar.Children.Add(_showNew);
        bar.Children.Add(_showSame);
        bar.Children.Add(_status);
        _cloudButton = new Button { Style = (Style)FindResource("ButtonPrimary"), Content = $"Änderungen als Wolken in {newLabel} eintragen", Margin = new Thickness(4, 0, 4, 0) };
        _cloudButton.Click += (_, _) => AddClouds();
        bar.Children.Add(_cloudButton);
        _status.SetResourceReference(TextBlock.ForegroundProperty, "TextSecondaryBrush");
        _pageText.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");

        var top = new Border { Child = bar, Padding = new Thickness(8, 6, 8, 6), BorderThickness = new Thickness(0, 0, 0, 1) };
        top.SetResourceReference(Border.BackgroundProperty, "Surface1Brush");
        top.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
        _image.LayoutTransform = _zoom;
        _scroll.Content = new Border { Child = _image, Margin = new Thickness(16), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        _scroll.PreviewMouseWheel += (_, e) =>
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                SetZoom(_zoom.ScaleX * (e.Delta > 0 ? 1.25 : 0.8));
                e.Handled = true;
            }
        };
        var dock = new DockPanel();
        DockPanel.SetDock(top, Dock.Top);
        dock.Children.Add(top);
        dock.Children.Add(_scroll);
        Content = dock;
        Loaded += (_, _) => ShowPage(0);
    }

    private int PageCount => Math.Min(_old.PageCount, _new.PageCount);

    /// <summary>Legendeneintrag, zugleich Schalter zum Ein- und Ausblenden der Kategorie.</summary>
    private CheckBox Legend(PdfColor color, string text)
    {
        var box = new CheckBox { IsChecked = true, Margin = new Thickness(8, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center, ToolTip = "Ein- oder ausblenden" };
        box.Click += (_, _) => RefreshImage();
        var panel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        box.Content = panel;
        panel.Children.Add(new Rectangle { Width = 12, Height = 12, Fill = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)), Margin = new Thickness(0, 0, 6, 0) });
        var label = new TextBlock { Text = text };
        label.SetResourceReference(TextBlock.ForegroundProperty, "TextBrush");
        panel.Children.Add(label);
        return box;
    }

    private void RefreshImage()
    {
        if (_result is null)
        {
            return;
        }
        var pixels = PageComparer.Filter(_result.Overlay, _showOld.IsChecked == true, _showNew.IsChecked == true, _showSame.IsChecked == true);
        var bitmap = BitmapSource.Create(_result.Width, _result.Height, 96, 96, PixelFormats.Bgra32, null, pixels, _result.Width * 4);
        bitmap.Freeze();
        _image.Source = bitmap;
    }

    private void ShowPage(int page)
    {
        if (PageCount == 0)
        {
            _status.Text = "Eine der Dateien hat keine Seiten.";
            _cloudButton.IsEnabled = _prev.IsEnabled = _next.IsEnabled = false;
            return;
        }
        _page = Math.Clamp(page, 0, PageCount - 1);
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            var scale = PageComparer.ScaleFor(_old, _page, _new, _page);
            _result = PageComparer.Compare(_old, _page, _new, _page, scale, align: true);
        }
        finally
        {
            Mouse.OverrideCursor = null;
        }
        RefreshImage();
        _pageText.Text = $"Seite {_page + 1} von {PageCount}";
        _prev.IsEnabled = _page > 0;
        _next.IsEnabled = _page < PageCount - 1;
        var n = _result.Changes.Count;
        _status.Text = n switch
        {
            0 => "Keine Unterschiede gefunden.",
            1 => "1 geänderter Bereich.",
            >= PageComparer.MaxChanges => $"Mehr als {PageComparer.MaxChanges} geänderte Bereiche, die größten werden gezeigt.",
            _ => $"{n} geänderte Bereiche.",
        };
        if (_result.OffsetX != 0 || _result.OffsetY != 0)
        {
            var (mx, my) = _result.OffsetMillimeters;
            _status.Text += $" Der neue Stand ist um {mx:0.#} mm waagerecht und {my:0.#} mm senkrecht verschoben, das wurde ausgeglichen.";
        }
        _cloudButton.IsEnabled = n > 0 && _new.CanAnnotate;
        FitPage();
    }

    private void FitPage()
    {
        if (_result is null || _scroll.ViewportWidth <= 0)
        {
            return;
        }
        SetZoom(Math.Min((_scroll.ViewportWidth - 40) / _result.Width, (_scroll.ViewportHeight - 40) / _result.Height));
    }

    private void SetZoom(double z)
    {
        z = Math.Clamp(z, 0.05, 8);
        _zoom.ScaleX = _zoom.ScaleY = z;
    }

    private void AddClouds()
    {
        if (_result is not { HasChanges: true } result)
        {
            return;
        }
        var clouds = PageComparer.ChangeClouds(result.Changes, Environment.UserName, $"Änderung gegenüber {_oldLabel}");
        try
        {
            if (!_addToOpen(_newPath, _page, clouds))
            {
                // Nicht geöffnet: direkt in die Datei schreiben. Die Ansicht in diesem Fenster bleibt beim gelesenen Stand.
                using var target = PdfDocument.Open(_newPath);
                var outliner = new Services.WpfTextOutliner();
                target.SetMarkups(_page, [.. target.GetMarkups(_page), .. clouds], outliner);
                target.Save();
            }
            _cloudButton.IsEnabled = false;
            _status.Text = $"{clouds.Count} Wolke(n) eingetragen.";
        }
        catch (Exception ex) when (ex is PdfException or IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(this, $"Die Wolken konnten nicht eingetragen werden: {ex.Message}", "EGLE360 PLAN", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
