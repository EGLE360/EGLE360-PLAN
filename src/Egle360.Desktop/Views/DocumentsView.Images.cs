using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Egle360.Desktop.Controls;
using Egle360.Pdf.Markups;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>Bildmarkierungen: Bild aus Datei oder Zwischenablage und gezeichnete Unterschriftsbilder.</summary>
public partial class DocumentsView
{
    /// <summary>Längste Kante eingefügter Bilder in Pixel; größere werden verkleinert, damit die PDF-Datei klein bleibt.</summary>
    private const int ImportMaxSide = 1200;

    private const string SignatureSet = "Unterschriften";

    /// <summary>Auswahl unter der Bildschaltfläche. Ohne Wahl geht es zurück zur Auswahl.</summary>
    private void ShowImageMenu(RadioButton button)
    {
        if (_active is not { } open)
        {
            return;
        }
        var chosen = false;
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        void Add(string header, string tip, bool enabled, Func<MarkupImage?> load)
        {
            var item = new MenuItem { Header = header, ToolTip = tip, IsEnabled = enabled };
            item.Click += (_, _) =>
            {
                chosen = true;
                if (load() is { } image)
                {
                    open.Viewer.StartImage(image);
                    open.Viewer.FocusView();
                }
                else
                {
                    open.Viewer.SetTool(ViewerTool.Select);
                    SyncToolButtons(open.Viewer);
                }
            };
            menu.Items.Add(item);
        }
        Add("Bild aus Datei …", "PNG, JPEG, BMP, TIFF oder GIF. Danach klicken oder einen Rahmen aufziehen.", true, LoadImageFile);
        Add("Bild aus der Zwischenablage", "Zum Beispiel ein Bildschirmausschnitt oder ein kopiertes Foto.", Clipboard.ContainsImage(),
            () => Clipboard.GetImage() is { } source ? ToMarkupImage(source) : null);
        Add("Unterschrift zeichnen …", "Mit Maus, Stift oder Finger zeichnen. Das ergibt ein Unterschriftsbild, keine digitale Signatur.", true,
            DrawSignature);
        var saved = App.Tools.Library.Sets.FirstOrDefault(s => s.Name == SignatureSet)?.Tools.Where(t => t.Image is not null).ToList() ?? [];
        if (saved.Count > 0)
        {
            menu.Items.Add(new Separator());
            foreach (var tool in saved)
            {
                Add(tool.Name, "Gespeichertes Unterschriftsbild aus der Werkzeugbibliothek.", true, () => tool.Image);
            }
        }
        menu.Closed += (_, _) =>
        {
            if (!chosen && open.Viewer.PendingImage is null && open.Viewer.ActivePreset is null)
            {
                open.Viewer.SetTool(ViewerTool.Select);
                SyncToolButtons(open.Viewer);
            }
        };
        menu.IsOpen = true;
    }

    private MarkupImage? LoadImageFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Bilder (*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff;*.gif|Alle Dateien (*.*)|*.*",
        };
        if (dialog.ShowDialog(Owner) != true)
        {
            return null;
        }
        try
        {
            using var stream = File.OpenRead(dialog.FileName);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            return ToMarkupImage(frame);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException or ArgumentException)
        {
            ShowError($"Das Bild konnte nicht gelesen werden: {ex.Message}");
            return null;
        }
    }

    /// <summary>Wandelt ein WPF-Bild in 32-Bit-Pixel um und verkleinert es auf <see cref="ImportMaxSide"/>.</summary>
    internal static MarkupImage ToMarkupImage(BitmapSource source)
    {
        var longest = Math.Max(source.PixelWidth, source.PixelHeight);
        if (longest > ImportMaxSide)
        {
            var f = (double)ImportMaxSide / longest;
            source = new TransformedBitmap(source, new ScaleTransform(f, f));
        }
        var converted = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        int w = converted.PixelWidth, h = converted.PixelHeight;
        var pixels = new byte[w * h * 4];
        converted.CopyPixels(pixels, w * 4, 0);
        // Manche Programme legen Bilder mit durchweg Alpha 0 in die Zwischenablage; die wären unsichtbar.
        var anyVisible = false;
        for (var i = 3; i < pixels.Length && !anyVisible; i += 4)
        {
            anyVisible = pixels[i] != 0;
        }
        if (!anyVisible)
        {
            for (var i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 255;
            }
        }
        return new MarkupImage(w, h, pixels);
    }

    /// <summary>Zeichenfläche für ein Unterschriftsbild; auf Wunsch wird es in der Werkzeugbibliothek gespeichert.</summary>
    private MarkupImage? DrawSignature()
    {
        var ink = new InkCanvas
        {
            Width = 480,
            Height = 180,
            Background = Brushes.White,
            DefaultDrawingAttributes = new DrawingAttributes
            {
                Color = Color.FromRgb(20, 40, 110),
                Width = 2.6,
                Height = 2.6,
                FitToCurve = true,
            },
        };
        var line = new Border { Height = 1, Background = Brushes.LightGray, Margin = new Thickness(24, 0, 24, 40), VerticalAlignment = VerticalAlignment.Bottom, IsHitTestVisible = false };
        var pad = new Grid();
        pad.Children.Add(ink);
        pad.Children.Add(line);
        var clear = new Button { Style = (Style)FindResource("ButtonSecondary"), Content = "Neu zeichnen", Margin = new Thickness(0, 0, 12, 0) };
        clear.Click += (_, _) => ink.Strokes.Clear();
        var keep = new CheckBox { Content = "In der Werkzeugbibliothek speichern", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0) };
        row.Children.Add(clear);
        row.Children.Add(keep);
        var error = new TextBlock { Style = (Style)FindResource("TextCaption"), Margin = new Thickness(0, 6, 0, 0), Visibility = Visibility.Collapsed, Text = "Bitte zuerst unterschreiben." };
        var content = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
        content.Children.Add(new Border { BorderBrush = Brushes.Gray, BorderThickness = new Thickness(1), Child = pad, HorizontalAlignment = HorizontalAlignment.Left });
        content.Children.Add(row);
        content.Children.Add(error);
        var answer = Dialogs.ShowContent(Owner, "Unterschrift zeichnen",
            "Unterschreiben Sie mit Maus, Stift oder Finger. Das Ergebnis ist ein Bild der Unterschrift, keine digitale Signatur im rechtlichen Sinn.",
            content, "Übernehmen", () =>
            {
                var ok = ink.Strokes.Count > 0;
                error.Visibility = ok ? Visibility.Collapsed : Visibility.Visible;
                return ok;
            });
        if (!answer || ink.Strokes.Count == 0)
        {
            return null;
        }
        var image = RenderStrokes(ink.Strokes);
        if (keep.IsChecked == true)
        {
            var count = App.Tools.Library.Sets.FirstOrDefault(s => s.Name == SignatureSet)?.Tools.Count ?? 0;
            var name = count == 0 ? $"Unterschrift {Environment.UserName}" : $"Unterschrift {Environment.UserName} {count + 1}";
            var tool = new ToolPreset(ToolPreset.NewId(), name, MarkupKind.Image, MarkupStyle.DefaultFor(MarkupKind.Image), Image: image);
            UpdateTools(App.Tools.Library.Add(SignatureSet, tool));
        }
        return image;
    }

    /// <summary>Striche als Bild mit durchsichtigem Hintergrund, auf die Striche zugeschnitten, in dreifacher Auflösung.</summary>
    internal static MarkupImage RenderStrokes(StrokeCollection strokes)
    {
        const double scale = 3;
        var bounds = strokes.GetBounds();
        bounds.Inflate(4, 4);
        var w = Math.Clamp((int)Math.Ceiling(bounds.Width * scale), 1, MarkupImage.MaxSide);
        var h = Math.Clamp((int)Math.Ceiling(bounds.Height * scale), 1, MarkupImage.MaxSide);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new ScaleTransform(w / bounds.Width, h / bounds.Height));
            dc.PushTransform(new TranslateTransform(-bounds.X, -bounds.Y));
            strokes.Draw(dc);
        }
        var bitmap = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return ToMarkupImage(bitmap);
    }
}
