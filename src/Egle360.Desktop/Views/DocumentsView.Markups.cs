using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Egle360.Desktop.Controls;
using Egle360.Desktop.Services;
using Egle360.Pdf;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Views;

/// <summary>Markierungsleiste: Werkzeuge, Farbe, Linienbreite, Deckkraft, Füllung, Rückgängig und Wiederholen.</summary>
public partial class DocumentsView
{
    private static readonly (string Name, PdfColor Color)[] Palette =
    [
        ("Rot", new PdfColor(200, 30, 30)),
        ("Blau", new PdfColor(22, 119, 210)),
        ("Grün", new PdfColor(30, 140, 60)),
        ("Orange", new PdfColor(230, 120, 0)),
        ("Violett", new PdfColor(150, 60, 190)),
        ("Gelb", new PdfColor(255, 214, 10)),
        ("Schwarz", new PdfColor(30, 30, 30)),
    ];

    private static readonly double[] Widths = [0.5, 1, 1.5, 2, 3, 5];
    private static readonly double[] Opacities = [1, 0.75, 0.5, 0.25];

    /// <summary>Werkzeuge mit Symbol (16 × 16, Strichzeichnung) und Tastenkürzel.</summary>
    private static readonly (string Key, string Name, string Icon, Key? Shortcut)[] Tools =
    [
        ("select", "Auswählen", "M3.5,1.5 L3.5,13.5 L6.8,10.4 L9,15 L11,14 L8.8,9.5 L13,9.5 Z", Key.V),
        ("pan", "Ansicht verschieben", "M5,8 V3.5 M7.5,7.5 V2 M10,7.5 V2.5 M12.5,8.5 V4.5 M5,8 C4,7 2.5,7.5 3,9 L5.5,13 C6.5,14.5 8,15 9.5,15 C11.5,15 12.5,13.5 12.5,11.5 V8.5", Key.H),
        (nameof(MarkupKind.Rectangle), "Rechteck", "M2,3.5 H14 V12.5 H2 Z", Key.R),
        (nameof(MarkupKind.Ellipse), "Ellipse", "M2,8 A6,4.5 0 1 0 14,8 A6,4.5 0 1 0 2,8 Z", Key.E),
        (nameof(MarkupKind.Line), "Linie", "M2,14 L14,2", Key.L),
        (nameof(MarkupKind.Arrow), "Pfeil", "M2,14 L14,2 M8,2 H14 V8", Key.A),
        (nameof(MarkupKind.Polyline), "Polylinie (Abschluss mit Doppelklick oder Eingabetaste)", "M1.5,13 L5,4 L10,11 L14.5,3", Key.N),
        (nameof(MarkupKind.Polygon), "Polygon (Abschluss mit Doppelklick oder Eingabetaste)", "M8,1.5 L14.5,6.5 L12,14.5 H4 L1.5,6.5 Z", Key.P),
        (nameof(MarkupKind.Cloud), "Wolke (Abschluss mit Doppelklick oder Eingabetaste)", "M3.5,11 A2.5,2.5 0 0 1 2.5,6 A3,3 0 0 1 7.5,3.5 A3,3 0 0 1 12.5,5 A2.5,2.5 0 0 1 13,10.5 A3,3 0 0 1 8,12.5 A3,3 0 0 1 3.5,11 Z", Key.C),
        (nameof(MarkupKind.Freehand), "Freihand", "M1.5,11 C4,3 6,3 7,8 C8,13 10,13 14.5,4", Key.F),
        (nameof(MarkupKind.TextBox), "Textfeld", "M1.5,2.5 H14.5 V13.5 H1.5 Z M5,5.5 H11 M8,5.5 V11", Key.T),
        (nameof(MarkupKind.Typewriter), "Schreibmaschine (Text ohne Rahmen, klicken und schreiben)", "M3,3 H13 M8,3 V13 M6,13 H10", Key.W),
        (nameof(MarkupKind.Callout), "Sprechblase (von der Pfeilspitze zum Textfeld ziehen)", "M6.5,1.5 H14.5 V8 H6.5 Z M8.5,4.75 H12.5 M6.5,6.5 L1.5,14.5 M1.5,11 V14.5 H5", Key.B),
        (nameof(MarkupKind.Note), "Notiz", "M2,2.5 H14 V10.5 H7 L4,13.5 L4.5,10.5 H2 Z M4.5,5 H11.5 M4.5,7.5 H9.5", Key.K),
        (nameof(MarkupKind.Highlight), "Hervorhebung (folgt dem Text, sonst Rechteck)", "M2.5,14 H13.5 M5,11 L10.5,2.5 L13,4 L7.5,12.5 Z", Key.M),
        (nameof(MarkupKind.Underline), "Text unterstreichen (über den Text ziehen)", "M4,2 V8 A4,4 0 0 0 12,8 V2 M2.5,14.5 H13.5", Key.U),
        (nameof(MarkupKind.StrikeOut), "Text durchstreichen (über den Text ziehen)", "M11.5,3.5 C10,2 6,2 5,4 C4,6 6,7 8,7.5 M8,10 C10,10.5 12,11.5 11,13 C10,14.5 6,14.5 4.5,12.5 M2,8.5 H14", Key.X),
        (nameof(MarkupKind.Stamp), "Stempel (Klick setzt, Aufziehen bestimmt die Größe)", "M1.5,4 H14.5 V12 H1.5 Z M3,5.5 H13 V10.5 H3 Z M5,8 H11", Key.S),
        (nameof(MarkupKind.Image), "Bild oder Unterschrift einfügen", "M1.5,2.5 H14.5 V13.5 H1.5 Z M1.5,11 L5.5,7 L9,10.5 L11,8.5 L14.5,12 M10.5,5.5 A1.25,1.25 0 1 0 13,5.5 A1.25,1.25 0 1 0 10.5,5.5 Z", Key.I),
        (nameof(MarkupKind.Dimension), "Bemaßung", "M1.5,10 H14.5 M1.5,7 V13 M14.5,7 V13 M4.5,8.5 L1.5,10 L4.5,11.5 M11.5,8.5 L14.5,10 L11.5,11.5 M6,5 H10", Key.D),
        (nameof(MarkupKind.Length), "Länge messen", "M2,10 H14 M2,7 V13 M14,7 V13 M5,10 V8.5 M8,10 V7.5 M11,10 V8.5", null),
        (nameof(MarkupKind.Polylength), "Polylänge messen (Abschluss mit Doppelklick oder Eingabetaste)", "M2,13.5 L6,5 L10,10 L14,2.5 M1,12 L3,15 M13,1.5 L15,3.5", null),
        (nameof(MarkupKind.Perimeter), "Umfang messen (Abschluss mit Doppelklick oder Eingabetaste)", "M3.5,3.5 H12.5 V12.5 H3.5 Z M1.5,1.5 L5.5,5.5 M10.5,10.5 L14.5,14.5", null),
        (nameof(MarkupKind.Area), "Fläche messen (Abschluss mit Doppelklick oder Eingabetaste)", "M2.5,2.5 H13.5 V13.5 H2.5 Z M2.5,8 L8,2.5 M2.5,13.5 L13.5,2.5 M8,13.5 L13.5,8", null),
        (nameof(MarkupKind.Angle), "Winkel messen (drei Punkte: Schenkel, Scheitel, Schenkel)", "M14,13.5 H2.5 L11,3 M7,13.5 A4.5,4.5 0 0 0 5.5,10", null),
        (nameof(MarkupKind.Radius), "Radius messen (erst Mittelpunkt, dann Punkt auf dem Kreis)", "M8,8 m-6,0 a6,6 0 1 0 12,0 a6,6 0 1 0 -12,0 M8,8 L12.2,3.8 M6.5,8 H9.5 M8,6.5 V9.5", null),
        (nameof(MarkupKind.Diameter), "Durchmesser messen (zwei gegenüberliegende Punkte)", "M8,8 m-6,0 a6,6 0 1 0 12,0 a6,6 0 1 0 -12,0 M3.8,12.2 L12.2,3.8", null),
        (nameof(MarkupKind.Count), "Anzahl zählen (Abschluss mit Eingabetaste)", "M2.5,5 A1.5,1.5 0 1 0 5.5,5 A1.5,1.5 0 1 0 2.5,5 Z M10.5,5 A1.5,1.5 0 1 0 13.5,5 A1.5,1.5 0 1 0 10.5,5 Z M6.5,11.5 A1.5,1.5 0 1 0 9.5,11.5 A1.5,1.5 0 1 0 6.5,11.5 Z", null),
    ];

    private readonly WpfTextOutliner _outliner = new();
    private readonly Dictionary<string, RadioButton> _toolButtons = new();
    private readonly List<RadioButton> _swatches = [];
    private Button _widthButton = null!;
    private Button _scaleButton = null!;
    private ToggleButton _snapButton = null!;
    private ToggleButton _hideMarkupsButton = null!;
    private bool _snap = true;
    private Button _opacityButton = null!;
    private ToggleButton _fillButton = null!;
    private Button _undoButton = null!;
    private Button _redoButton = null!;
    private Button _deleteMarkupButton = null!;
    private PdfColor _color = Palette[0].Color;
    private double _width = 1.5;
    private double _opacity = 1;
    private bool _fill;
    private bool _syncingTools;
    private StampPreset _stamp = StampPreset.Defaults[0];

    private void BuildMarkupBar()
    {
        foreach (var (key, name, icon, shortcut) in Tools)
        {
            var button = new RadioButton
            {
                Style = (Style)FindResource("ToolToggle"),
                GroupName = "MarkupTools",
                Content = new Path { Style = (Style)FindResource("ToolIcon"), Data = Geometry.Parse(icon) },
                ToolTip = shortcut is { } k ? $"{name} ({k})" : name,
                Tag = key,
            };
            AutomationPropertiesHelper.SetName(button, name);
            button.Checked += OnToolChecked;
            if (key == nameof(MarkupKind.Stamp))
            {
                button.Click += (_, _) => ShowStampMenu(button);
            }
            if (key == nameof(MarkupKind.Image))
            {
                button.Click += (_, _) => ShowImageMenu(button);
            }
            _toolButtons[key] = button;
            if (key == nameof(MarkupKind.Length))
            {
                MarkupBar.Children.Add(Separator());
            }
            MarkupBar.Children.Add(button);
            if (key == "pan")
            {
                MarkupBar.Children.Add(Separator());
            }
        }
        _scaleButton = TextButton("Maßstab der Seite", OnScaleMenu);
        MarkupBar.Children.Add(_scaleButton);
        _snapButton = new ToggleButton
        {
            Style = (Style)FindResource("ToolToggle"),
            Content = new Path { Style = (Style)FindResource("ToolIcon"), Data = Geometry.Parse("M5,5 H11 V11 H5 Z M8,1 V5 M8,11 V15 M1,8 H5 M11,8 H15") },
            ToolTip = "Objektfang: Punkte rasten beim Zeichnen an Linienenden und -mitten des Plans und an Eckpunkten von Markierungen ein",
            IsChecked = _snap,
        };
        AutomationPropertiesHelper.SetName(_snapButton, "Objektfang");
        _snapButton.Click += (_, _) =>
        {
            _snap = _snapButton.IsChecked == true;
            foreach (var open in _documents)
            {
                open.Viewer.SnapEnabled = _snap;
            }
        };
        MarkupBar.Children.Add(_snapButton);
        _toolButtons["select"].IsChecked = true;

        MarkupBar.Children.Add(Separator());
        foreach (var (name, color) in Palette)
        {
            var swatch = new RadioButton
            {
                Style = (Style)FindResource("ColorSwatch"),
                GroupName = "MarkupColor",
                Background = new SolidColorBrush(Color.FromRgb(color.R, color.G, color.B)),
                ToolTip = $"Farbe {name}",
                Tag = color,
                IsChecked = color == _color,
            };
            AutomationPropertiesHelper.SetName(swatch, $"Farbe {name}");
            swatch.Checked += (_, _) => OnColorChosen(color);
            _swatches.Add(swatch);
            MarkupBar.Children.Add(swatch);
        }

        MarkupBar.Children.Add(Separator());
        _widthButton = TextButton("Linienbreite", OnWidthMenu);
        _opacityButton = TextButton("Deckkraft", OnOpacityMenu);
        _fillButton = new ToggleButton
        {
            Style = (Style)FindResource("ToolToggle"),
            Content = new Path { Style = (Style)FindResource("ToolIcon"), Data = Geometry.Parse("M2,3.5 H14 V12.5 H2 Z M2,7 L5.5,3.5 M2,11 L9.5,3.5 M4.5,12.5 L13.5,3.5 M8.5,12.5 L14,7"), },
            ToolTip = "Fläche füllen",
        };
        AutomationPropertiesHelper.SetName(_fillButton, "Fläche füllen");
        _fillButton.Click += (_, _) => OnFillToggled(_fillButton.IsChecked == true);
        MarkupBar.Children.Add(_widthButton);
        MarkupBar.Children.Add(_opacityButton);
        MarkupBar.Children.Add(_fillButton);

        MarkupBar.Children.Add(Separator());
        _undoButton = GlyphButton("", "Rückgängig (Strg+Z)", (_, _) => Undo());
        _redoButton = GlyphButton("", "Wiederholen (Strg+Y)", (_, _) => Redo());
        _deleteMarkupButton = GlyphButton("", "Markierung löschen (Entf)", (_, _) => _active?.Viewer.DeleteSelection());
        MarkupBar.Children.Add(_undoButton);
        MarkupBar.Children.Add(_redoButton);
        MarkupBar.Children.Add(_deleteMarkupButton);
        _hideMarkupsButton = new ToggleButton
        {
            Style = (Style)FindResource("ToolToggle"),
            Content = new Path { Style = (Style)FindResource("ToolIcon"), Data = Geometry.Parse("M1.5,8 C4,3.5 12,3.5 14.5,8 C12,12.5 4,12.5 1.5,8 Z M6,8 A2,2 0 1 0 10,8 A2,2 0 1 0 6,8 Z M2.5,14 L13.5,2") },
            ToolTip = "Markierungen ausblenden, um den Plan ohne sie zu sehen",
        };
        AutomationPropertiesHelper.SetName(_hideMarkupsButton, "Markierungen ausblenden");
        _hideMarkupsButton.Click += (_, _) =>
        {
            if (_active is { } open)
            {
                open.Viewer.MarkupsVisible = _hideMarkupsButton.IsChecked != true;
            }
        };
        MarkupBar.Children.Add(_hideMarkupsButton);
        RefreshStyleButtons();
    }

    private Border Separator() => new() { Style = (Style)FindResource("ToolSeparator") };

    private Button GlyphButton(string glyph, string tooltip, RoutedEventHandler click)
    {
        var b = new Button { Style = (Style)FindResource("ToolButton"), Content = glyph, ToolTip = tooltip };
        AutomationPropertiesHelper.SetName(b, tooltip);
        b.Click += click;
        return b;
    }

    private Button TextButton(string name, RoutedEventHandler click)
    {
        var b = new Button
        {
            Style = (Style)FindResource("ToolButton"),
            FontFamily = (FontFamily)FindResource("FontFamilyUi"),
            FontSize = 12,
            Width = double.NaN,
            Padding = new Thickness(8, 0, 8, 0),
            MinWidth = 58,
            ToolTip = name,
        };
        AutomationPropertiesHelper.SetName(b, name);
        b.Click += click;
        return b;
    }

    // ---------- Darstellung neuer und ausgewählter Markierungen ----------

    /// <summary>Darstellung für neue Markierungen aus den Einstellungen der Leiste.</summary>
    private MarkupStyle CurrentStyle(MarkupKind kind)
    {
        var d = MarkupStyle.DefaultFor(kind);
        return kind switch
        {
            MarkupKind.Highlight => d with { Stroke = _color, Fill = _color, Opacity = _opacity < 1 ? _opacity : 0.4 },
            MarkupKind.Note => d with { Fill = _color == Palette[0].Color ? d.Fill : _color, Opacity = _opacity },
            MarkupKind.TextBox => d with { Stroke = _color, LineWidth = _width, Opacity = _opacity, Fill = _fill ? PdfColor.White : null },
            MarkupKind.Callout => d with { Stroke = _color, LineWidth = _width, Opacity = _opacity },
            MarkupKind.Typewriter => d with { Stroke = _color, Opacity = _opacity },
            MarkupKind.Underline or MarkupKind.StrikeOut => d with { Stroke = _color, Opacity = _opacity },
            MarkupKind.Image => d with { Stroke = _color },
            MarkupKind.Stamp => d with { Stroke = _stamp.Color, Opacity = _opacity < 1 ? _opacity : d.Opacity, Fill = _fill ? PdfColor.White : null },
            _ => d with { Stroke = _color, LineWidth = _width, Opacity = _opacity, Fill = _fill && MarkupKinds.CanFill(kind) ? Tint(_color) : null },
        };
    }

    /// <summary>Helle Füllfarbe zur Linienfarbe (20 % Farbe, 80 % Weiß).</summary>
    private static PdfColor Tint(PdfColor c) => new((byte)(c.R * 0.2 + 204), (byte)(c.G * 0.2 + 204), (byte)(c.B * 0.2 + 204));

    private void OnColorChosen(PdfColor color)
    {
        _color = color;
        _active?.Viewer.ApplyToSelection(m => m.Kind switch
        {
            MarkupKind.Highlight => m.WithStyle(m.Style with { Stroke = color, Fill = color }),
            MarkupKind.Note => m.WithStyle(m.Style with { Fill = color }),
            MarkupKind.TextBox or MarkupKind.Callout or MarkupKind.Stamp or MarkupKind.Typewriter => m.WithStyle(m.Style with { Stroke = color }),
            _ => m.WithStyle(m.Style with { Stroke = color, Fill = m.Style.Fill is null ? null : Tint(color) }),
        }, "Farbe ändern");
    }

    private void OnWidthMenu(object sender, RoutedEventArgs e) =>
        ShowChoiceMenu(_widthButton, Widths, _width, w => $"{w.ToString("0.#", CultureInfo.CurrentCulture)} pt", w =>
        {
            _width = w;
            _active?.Viewer.ApplyToSelection(m => m.Kind is MarkupKind.Highlight or MarkupKind.Note or MarkupKind.Typewriter ? m : m.WithStyle(m.Style with { LineWidth = w }), "Linienbreite ändern");
            RefreshStyleButtons();
        });

    private void OnOpacityMenu(object sender, RoutedEventArgs e) =>
        ShowChoiceMenu(_opacityButton, Opacities, _opacity, o => $"{o * 100:0} %", o =>
        {
            _opacity = o;
            _active?.Viewer.ApplyToSelection(m => m.WithStyle(m.Style with { Opacity = o }), "Deckkraft ändern");
            RefreshStyleButtons();
        });

    private void OnFillToggled(bool fill)
    {
        _fill = fill;
        _active?.Viewer.ApplyToSelection(m => !MarkupKinds.CanFill(m.Kind) ? m
            : m.WithStyle(m.Style with { Fill = fill ? (m.Kind is MarkupKind.TextBox or MarkupKind.Callout or MarkupKind.Stamp ? PdfColor.White : Tint(m.Style.Stroke)) : null }), fill ? "Füllung hinzufügen" : "Füllung entfernen");
    }

    private void ShowChoiceMenu(Button target, double[] values, double current, Func<double, string> label, Action<double> choose)
    {
        var menu = new ContextMenu { PlacementTarget = target, Placement = PlacementMode.Bottom };
        foreach (var v in values)
        {
            var item = new MenuItem { Header = label(v), IsCheckable = true, IsChecked = Math.Abs(v - current) < 1e-9 };
            item.Click += (_, _) => choose(v);
            menu.Items.Add(item);
        }
        menu.IsOpen = true;
    }

    private void RefreshStyleButtons()
    {
        _widthButton.Content = $"{_width.ToString("0.#", CultureInfo.CurrentCulture)} pt";
        _opacityButton.Content = $"{_opacity * 100:0} %";
        _fillButton.IsChecked = _fill;
    }

    // ---------- Werkzeuge ----------

    private void OnToolChecked(object sender, RoutedEventArgs e)
    {
        if (_syncingTools || sender is not RadioButton { Tag: string key } || _active is null)
        {
            return;
        }
        ApplyTool(_active.Viewer, key);
        _active.Viewer.FocusView();
    }

    /// <summary>Auswahl der Stempelaufschrift unter der Schaltfläche; die Wahl gilt für die nächsten Stempel.</summary>
    private void ShowStampMenu(RadioButton button)
    {
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        foreach (var preset in StampPreset.Defaults)
        {
            var item = new MenuItem
            {
                Header = preset.Title,
                IsCheckable = true,
                IsChecked = preset == _stamp,
                Foreground = new SolidColorBrush(Color.FromRgb(preset.Color.R, preset.Color.G, preset.Color.B)),
                FontWeight = FontWeights.SemiBold,
            };
            item.Click += (_, _) => _stamp = preset;
            menu.Items.Add(item);
        }
        menu.Items.Add(new Separator());
        var custom = new MenuItem { Header = "Eigene Aufschrift …", ToolTip = "Aufschrift frei eingeben; Farbe ist die gewählte Markierungsfarbe." };
        custom.Click += (_, _) =>
        {
            var title = Dialogs.AskText(Owner, "Eigener Stempel", "Aufschrift des Stempels, zum Beispiel BAUSTELLE BESICHTIGT. Darunter stehen automatisch Autor und Datum.",
                StampPreset.Defaults.Contains(_stamp) ? "" : _stamp.Title);
            if (!string.IsNullOrWhiteSpace(title))
            {
                _stamp = new StampPreset(title.Trim(), _color);
            }
        };
        menu.Items.Add(custom);
        menu.IsOpen = true;
    }

    private static void ApplyTool(PdfViewer viewer, string key)
    {
        switch (key)
        {
            case "select":
                viewer.SetTool(ViewerTool.Select);
                break;
            case "pan":
                viewer.SetTool(ViewerTool.Pan);
                break;
            default:
                viewer.SetTool(ViewerTool.Draw, Enum.Parse<MarkupKind>(key));
                break;
        }
    }

    private string CheckedToolKey => _toolButtons.FirstOrDefault(t => t.Value.IsChecked == true).Key ?? "select";

    /// <summary>Werkzeugleiste an das Werkzeug der Ansicht angleichen (z. B. nach dem Zeichnen zurück zur Auswahl).</summary>
    private void SyncToolButtons(PdfViewer viewer)
    {
        var key = viewer.Tool switch
        {
            ViewerTool.Pan => "pan",
            ViewerTool.Draw => viewer.DrawKind.ToString(),
            _ => "select",
        };
        _syncingTools = true;
        _toolButtons[key].IsChecked = true;
        _syncingTools = false;
    }

    private void RefreshMarkupBar()
    {
        var doc = _active?.Document;
        var canAnnotate = doc is { CanAnnotate: true };
        MarkupBarBorder.Visibility = _active is null ? Visibility.Collapsed : Visibility.Visible;
        foreach (var (key, button) in _toolButtons)
        {
            button.IsEnabled = key is "select" or "pan" || canAnnotate;
        }
        foreach (var swatch in _swatches)
        {
            swatch.IsEnabled = canAnnotate;
        }
        _widthButton.IsEnabled = _opacityButton.IsEnabled = _fillButton.IsEnabled = canAnnotate;
        var history = _active?.History;
        _undoButton.IsEnabled = history?.CanUndo == true;
        _redoButton.IsEnabled = history?.CanRedo == true;
        _undoButton.ToolTip = history?.UndoDescription is { } u ? $"Rückgängig: {u} (Strg+Z)" : "Rückgängig (Strg+Z)";
        _redoButton.ToolTip = history?.RedoDescription is { } r ? $"Wiederholen: {r} (Strg+Y)" : "Wiederholen (Strg+Y)";
        var selected = _active?.Viewer.SelectedMarkups ?? [];
        _deleteMarkupButton.IsEnabled = canAnnotate && selected.Count > 0;
        _hideMarkupsButton.IsChecked = _active?.Viewer.MarkupsVisible == false;
        StatusMarkup.Text = selected.Count switch
        {
            0 => "",
            1 when MarkupKinds.IsMeasurement(selected[0].Kind) => $"{MarkupKinds.DisplayName(selected[0].Kind)}: {Measurement.Describe(selected[0])}"
                + (selected[0].Scale is { } sc ? $", Maßstab {sc.Label}" : "") + StatusSuffix(selected[0]),
            1 => $"{MarkupKinds.DisplayName(selected[0].Kind)} ausgewählt, {selected[0].Author}, {selected[0].Modified.LocalDateTime:g}" + StatusSuffix(selected[0]),
            _ => $"{selected.Count} Markierungen ausgewählt" + SelectionTotals(selected),
        };
        RefreshScaleButton(canAnnotate);
        if (_active is not null && !canAnnotate)
        {
            StatusMarkup.Text = "Markierungen sind laut Berechtigungen des Dokuments nicht erlaubt.";
        }
    }

    /// <summary>Status und Sperre für die Statuszeile, z. B. „, Status Geprüft (Elias), gesperrt“.</summary>
    private static string StatusSuffix(Markup m) =>
        (m.Status is { } s ? $", Status {s.Name}" + (s.By.Length > 0 ? $" ({s.By})" : "") : "") + (m.Locked ? ", gesperrt" : "");

    private void Undo()
    {
        if (_active?.History.Undo() is { } page)
        {
            _active.Viewer.GoToPageIfHidden(page);
        }
    }

    private void Redo()
    {
        if (_active?.History.Redo() is { } page)
        {
            _active.Viewer.GoToPageIfHidden(page);
        }
    }

    private bool HandleMarkupShortcut(KeyEventArgs e)
    {
        if (Keyboard.FocusedElement is TextBox or PasswordBox)
        {
            return false;
        }
        if (KeyGestures.Ctrl(e, Key.Z)) { Undo(); return true; }
        if (KeyGestures.Ctrl(e, Key.Y) || KeyGestures.Ctrl(e, Key.Z, shift: true)) { Redo(); return true; }
        if (_active is { } open)
        {
            if (KeyGestures.Ctrl(e, Key.C, shift: true)) { open.Viewer.CopyStyle(); return true; }
            if (KeyGestures.Ctrl(e, Key.V, shift: true)) { open.Viewer.PasteStyle(); return true; }
            if (KeyGestures.Ctrl(e, Key.C)) { open.Viewer.CopySelection(); return true; }
            if (KeyGestures.Ctrl(e, Key.V)) { open.Viewer.Paste(); return true; }
            if (KeyGestures.Ctrl(e, Key.D)) { open.Viewer.DuplicateSelection(); return true; }
            if (KeyGestures.Ctrl(e, Key.A)) { open.Viewer.SelectAllOnPage(); return true; }
        }
        if (Keyboard.Modifiers == ModifierKeys.None && _active is not null)
        {
            var tool = Tools.FirstOrDefault(t => t.Shortcut == e.Key);
            if (tool.Key is not null && _toolButtons[tool.Key].IsEnabled)
            {
                _toolButtons[tool.Key].IsChecked = true;
                if (tool.Key == nameof(MarkupKind.Image))
                {
                    ShowImageMenu(_toolButtons[tool.Key]);
                }
                return true;
            }
        }
        return false;
    }

    // ---------- Text bearbeiten ----------

    private void OnEditText(OpenDocument open, int page, Markup markup)
    {
        if (!open.Document.CanAnnotate || markup.Locked)
        {
            return;
        }
        var isText = MarkupKinds.HasText(markup.Kind);
        var text = Dialogs.AskMultilineText(Owner,
            isText ? $"{MarkupKinds.DisplayName(markup.Kind)} bearbeiten" : "Kommentar bearbeiten",
            isText ? "Text der Markierung. Zeilenumbrüche mit der Eingabetaste." : "Kommentar zur Markierung, wird im PDF als Inhalt gespeichert.",
            markup.Text);
        if (text is null || text == markup.Text)
        {
            return;
        }
        var current = open.Document.GetMarkups(page).FirstOrDefault(m => m.Id == markup.Id);
        if (current is null)
        {
            return;
        }
        var changed = current.WithText(text);
        if (MarkupKinds.HasTextFrame(changed.Kind))
        {
            changed = GrowToFitText(changed);
        }
        try
        {
            open.History.Replace(page, changed, isText ? "Text ändern" : "Kommentar ändern");
        }
        catch (PdfException ex)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>
    /// Vergrößert ein Textfeld nach unten (auf dem Bildschirm), bis der Text hineinpasst. Schreibmaschinentext bekommt
    /// genau die Größe des Textes.
    /// </summary>
    private Markup GrowToFitText(Markup m)
    {
        var box = MarkupGeometry.HandleRect(m);
        var frame = TextFrame.ForBox(box, m.TextRotation);
        if (m.Kind == MarkupKind.Typewriter)
        {
            var natural = _outliner.Layout(m.Text, m.Style.FontSize, 10_000);
            var size = new PdfPoint(natural.Width + 2 * MarkupGeometry.TextPadding + 1, natural.Height + 2 * MarkupGeometry.TextPadding);
            return MarkupGeometry.ResizeTo(m, PdfRect.FromPoints(frame.Origin, frame.ToUser(size)));
        }
        var outline = _outliner.Layout(m.Text, m.Style.FontSize, Math.Max(1, frame.Width - 2 * MarkupGeometry.TextPadding));
        var needed = outline.Height + 2 * MarkupGeometry.TextPadding;
        if (needed <= frame.Height)
        {
            return m;
        }
        var corner = frame.ToUser(new PdfPoint(frame.Width, needed));
        return MarkupGeometry.ResizeTo(m, PdfRect.FromPoints(frame.Origin, corner));
    }
}
