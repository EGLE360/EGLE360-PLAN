using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Egle360.Pdf;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Controls;

/// <summary>Zeichnen, Auswählen und Bearbeiten von Markierungen in der Ansicht.</summary>
public sealed partial class PdfViewer
{
    /// <summary>Fangbereich für Linien und Griffe in DIP.</summary>
    private const double HitTolerance = 5;

    /// <summary>Mindestweg in DIP, ab dem Ziehen als Ziehen gilt.</summary>
    private const double DragThreshold = 3;

    private MarkupHistory? _history;
    private ViewerTool _tool = ViewerTool.Select;
    private MarkupDrawing? _drawing;
    private int _drawingPage = -1;
    private int _selectedPage = -1;
    private readonly HashSet<string> _selected = [];
    private EditDrag? _drag;

    /// <summary>Laufendes Verschieben oder Ziehen an einem Griff.</summary>
    private sealed record EditDrag(int Page, Point StartLocal, IReadOnlyList<Markup> Originals, int Handle)
    {
        public bool Moved { get; set; }
    }

    public ITextOutliner Outliner { get; set; } = new BlockTextOutliner();

    /// <summary>Darstellung für neue Markierungen einer Art.</summary>
    public Func<MarkupKind, MarkupStyle> StyleProvider { get; set; } = MarkupStyle.DefaultFor;

    private bool _markupsVisible = true;

    /// <summary>Markierungen anzeigen. Ausgeblendete lassen sich weder anklicken noch bearbeiten.</summary>
    public bool MarkupsVisible
    {
        get => _markupsVisible;
        set
        {
            if (_markupsVisible == value)
            {
                return;
            }
            _markupsVisible = value;
            if (!value)
            {
                ClearSelection();
                if (_tool is ViewerTool.Draw)
                {
                    SetTool(ViewerTool.Select);
                }
            }
            foreach (var visual in _visuals.Values)
            {
                visual.Layer.SetMarkupsVisible(value);
            }
            MarkupsVisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? MarkupsVisibilityChanged;

    /// <summary>Kopierte Darstellung (Farbe, Linie, Füllung, Schrift) für „Darstellung einfügen“.</summary>
    private static MarkupStyle? s_styleClipboard;

    public static bool HasStyleClipboard => s_styleClipboard is not null;

    public void CopyStyle()
    {
        if (SelectedMarkups is { Count: > 0 } selected)
        {
            s_styleClipboard = selected[0].Style;
        }
    }

    /// <summary>Überträgt die kopierte Darstellung auf die Auswahl; Notizen und Hervorhebungen behalten ihre Eigenart.</summary>
    public void PasteStyle()
    {
        if (s_styleClipboard is not { } style)
        {
            return;
        }
        ApplyToSelection(m => m.WithStyle(m.Kind switch
        {
            MarkupKind.Highlight => m.Style with { Stroke = style.Stroke, Fill = style.Stroke, Opacity = style.Opacity },
            MarkupKind.Note => m.Style with { Fill = style.Fill ?? style.Stroke },
            _ => style with { Fill = MarkupKinds.CanFill(m.Kind) || m.Kind == MarkupKind.Stamp ? style.Fill : null },
        }), "Darstellung übertragen");
    }

    /// <summary>Richtet die ausgewählten, nicht gesperrten Markierungen aus.</summary>
    public void AlignSelection(MarkupAlignment alignment)
    {
        if (!CanAnnotate || _selectedPage < 0)
        {
            return;
        }
        var moved = MarkupArrange.Align(SelectedMarkups.Where(m => !m.Locked).ToList(), alignment, Transform(_selectedPage).Rotation);
        if (moved.Count > 0)
        {
            Run(() => _history!.ReplaceMany(_selectedPage, moved, MarkupArrange.DisplayName(alignment)));
        }
    }

    /// <summary>Dreht die ausgewählten, nicht gesperrten Markierungen gemeinsam um 90-Grad-Schritte im Uhrzeigersinn.</summary>
    public void RotateSelection(int quarterTurns)
    {
        var list = SelectedMarkups.Where(m => !m.Locked && MarkupRotation.CanRotate(m.Kind)).ToList();
        if (!CanAnnotate || list.Count == 0)
        {
            return;
        }
        var center = MarkupRotation.Center(list);
        Run(() => _history!.ReplaceMany(_selectedPage, [.. list.Select(m => MarkupRotation.RotateQuarter(m, quarterTurns, center))], "Drehen"));
    }

    /// <summary>Dreht die ausgewählten Markierungen um einen beliebigen Winkel gegen den Uhrzeigersinn.</summary>
    public void RotateSelectionFree(double degrees)
    {
        var list = SelectedMarkups.Where(m => !m.Locked && MarkupRotation.CanRotateFreely(m.Kind)).ToList();
        if (!CanAnnotate || list.Count == 0)
        {
            return;
        }
        var center = MarkupRotation.Center(list);
        Run(() => _history!.ReplaceMany(_selectedPage, [.. list.Select(m => MarkupRotation.RotateFree(m, degrees, center))], "Drehen"));
    }

    /// <summary>Ein Drehwinkel für die Auswahl soll eingegeben werden (Kontextmenü).</summary>
    public event EventHandler? RotateAngleRequested;

    /// <summary>Objektfang beim Zeichnen und beim Ziehen von Eckpunkten.</summary>
    public bool SnapEnabled { get; set; } = true;

    /// <summary>Fangradius in DIP auf dem Bildschirm.</summary>
    private const double SnapTolerance = 10;

    /// <summary>Fangpunkte des Planinhalts je Seite, beim ersten Gebrauch berechnet.</summary>
    private readonly Dictionary<int, SnapIndex> _snapIndexes = new();

    /// <summary>Anfangstext neuer Markierungen einer Art (Aufschrift des Stempels).</summary>
    public Func<MarkupKind, string> TextProvider { get; set; } = _ => "";

    /// <summary>Autor neuer Markierungen (PDF-Schlüssel T).</summary>
    public string Author { get; set; } = Environment.UserName;

    public MarkupKind DrawKind { get; private set; } = MarkupKind.Rectangle;

    /// <summary>
    /// Werkzeug aus der Bibliothek, mit dem gerade gezeichnet wird. Es bleibt nach jeder Markierung aktiv, bis Esc
    /// gedrückt oder ein anderes Werkzeug gewählt wird, damit sich gleiche Mengen schnell hintereinander erfassen lassen.
    /// </summary>
    public ToolPreset? ActivePreset { get; private set; }

    /// <summary>Werkzeug aus der Bibliothek zum Zeichnen wählen.</summary>
    public void StartPreset(ToolPreset preset)
    {
        SetTool(ViewerTool.Draw, preset.Kind);
        ActivePreset = preset;
        ToolChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Bild, das mit dem Bildwerkzeug als Nächstes eingesetzt wird.</summary>
    public MarkupImage? PendingImage { get; private set; }

    /// <summary>
    /// Bild einsetzen: ein Klick setzt es in Standardgröße, Aufziehen bestimmt den Rahmen. Danach ist das Bild
    /// ausgewählt und das Auswahlwerkzeug aktiv.
    /// </summary>
    public void StartImage(MarkupImage image)
    {
        SetTool(ViewerTool.Draw, MarkupKind.Image);
        PendingImage = image;
        ToolChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Aus einer Markierung soll ein Werkzeug der Bibliothek werden (Kontextmenü).</summary>
    public event EventHandler<Markup>? SaveAsToolRequested;

    /// <summary>Maßstab einer Seite für neue Messungen.</summary>
    public Func<int, MeasureScale?> ScaleProvider { get; set; } = _ => null;

    /// <summary>Eine Messung soll auf einer Seite ohne Maßstab beginnen (Argument: Seitenindex).</summary>
    public event EventHandler<int>? ScaleRequired;

    /// <summary>Kalibrierstrecke gesetzt: Seite und Länge der Strecke in Punkt.</summary>
    public event EventHandler<(int Page, double LengthPt)>? CalibrationMeasured;

    /// <summary>Fläche, zu der gerade ein Ausschnitt gezeichnet wird.</summary>
    private (int Page, string Id)? _cutout;

    /// <summary>Zeichnet als Nächstes ein Polygon, das von der Fläche abgezogen wird.</summary>
    public void StartCutout(int page, string id)
    {
        SetTool(ViewerTool.Draw, MarkupKind.Polygon);
        _cutout = (page, id);
    }

    /// <summary>Art, die beim aktuellen Werkzeug gezeichnet wird; beim Kalibrieren eine einfache Linie.</summary>
    private MarkupKind EffectiveKind => _tool == ViewerTool.Calibrate ? MarkupKind.Line : DrawKind;

    private static readonly MarkupStyle CalibrationStyle = new(new PdfColor(230, 120, 0), null, 1, 1.5);

    public ViewerTool Tool => _tool;

    /// <summary>Ausgelöst, wenn sich Auswahl oder Werkzeug ändern.</summary>
    public event EventHandler? SelectionChanged;

    public event EventHandler? ToolChanged;

    /// <summary>Text einer Markierung soll bearbeitet werden (Doppelklick, neues Textfeld, Kontextmenü).</summary>
    public event EventHandler<(int Page, Markup Markup)>? EditTextRequested;

    /// <summary>Einheitspreis einer Messung soll eingegeben werden (Kontextmenü).</summary>
    public event EventHandler<(int Page, Markup Markup)>? PriceRequested;

    /// <summary>Höhe oder Tiefe einer Messung soll eingegeben werden (Kontextmenü).</summary>
    public event EventHandler<(int Page, Markup Markup)>? DepthRequested;

    public event EventHandler<(int Page, Markup Markup)>? SlopeRequested;

    public int SelectedPage => _selected.Count > 0 ? _selectedPage : -1;

    public IReadOnlyList<Markup> SelectedMarkups =>
        _document is null || _selected.Count == 0 || _selectedPage < 0 || _selectedPage >= _layout.PageCount
            ? []
            : _document.GetMarkups(_selectedPage).Where(m => _selected.Contains(m.Id)).ToList();

    private bool CanAnnotate => _document is { CanAnnotate: true } && _history is not null;

    public void SetTool(ViewerTool tool, MarkupKind? kind = null)
    {
        CancelDrawing();
        ShowSnapMarker(-1, null);
        _cutout = null;
        ActivePreset = null;
        PendingImage = null;
        if (tool is ViewerTool.Draw or ViewerTool.Calibrate && !_markupsVisible)
        {
            MarkupsVisible = true;
        }
        _tool = tool;
        if (kind is { } k)
        {
            DrawKind = k;
        }
        if (tool is ViewerTool.Draw or ViewerTool.Calibrate)
        {
            ClearSelection();
        }
        UpdateCursor(null);
        ToolChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Select(int page, string id)
    {
        ClearSelection(raise: false);
        _selectedPage = page;
        _selected.Add(id);
        RefreshSelection();
    }

    public void ClearSelection() => ClearSelection(raise: true);

    private void ClearSelection(bool raise)
    {
        if (_selected.Count == 0)
        {
            return;
        }
        _selected.Clear();
        RefreshSelection(raise);
    }

    public void DeleteSelection()
    {
        if (!CanAnnotate || _selected.Count == 0)
        {
            return;
        }
        var page = _selectedPage;
        // Gesperrte Markierungen bleiben stehen und ausgewählt.
        var locked = SelectedMarkups.Where(m => m.Locked).Select(m => m.Id).ToHashSet();
        var ids = _selected.Where(id => !locked.Contains(id)).ToList();
        if (ids.Count == 0)
        {
            return;
        }
        ClearSelection(raise: false);
        Run(() => _history!.Remove(page, ids));
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Ändert alle ausgewählten Markierungen in einem Schritt (z. B. Farbe oder Linienbreite). Gesperrte bleiben
    /// unverändert, außer bei <paramref name="includeLocked"/> (Status und Sperre selbst).
    /// </summary>
    public void ApplyToSelection(Func<Markup, Markup> change, string description, bool includeLocked = false)
    {
        if (!CanAnnotate || _selected.Count == 0)
        {
            return;
        }
        var changed = SelectedMarkups.Where(m => includeLocked || !m.Locked).Select(change).ToList();
        if (changed.Count == 0)
        {
            return;
        }
        Run(() => _history!.ReplaceMany(_selectedPage, changed, description));
    }

    // ---------- Kopieren und Einfügen ----------

    /// <summary>Zwischenablage für Markierungen, für alle offenen Dokumente gemeinsam.</summary>
    private static IReadOnlyList<Markup> s_clipboard = [];
    private static int s_pasteCount;

    public static bool HasClipboard => s_clipboard.Count > 0;

    public void CopySelection()
    {
        if (SelectedMarkups is { Count: > 0 } selected)
        {
            s_clipboard = selected;
            s_pasteCount = 0;
        }
    }

    /// <summary>Fügt die kopierten Markierungen auf der aktuellen Seite ein, leicht versetzt.</summary>
    public void Paste()
    {
        s_pasteCount++;
        Insert(s_clipboard, s_pasteCount, s_clipboard.Count == 1 ? "Einfügen" : $"{s_clipboard.Count} Markierungen einfügen");
    }

    /// <summary>Verdoppelt die Auswahl auf derselben Seite, ohne die Zwischenablage zu ändern.</summary>
    public void DuplicateSelection()
    {
        if (_selected.Count > 0)
        {
            GoToPageIfHidden(_selectedPage);
            Insert(SelectedMarkups, 1, "Duplizieren", _selectedPage);
        }
    }

    public void SelectAllOnPage()
    {
        if (_document is null || _currentPage < 0)
        {
            return;
        }
        ClearSelection(raise: false);
        _selectedPage = _currentPage;
        foreach (var m in _document.GetMarkups(_currentPage))
        {
            _selected.Add(m.Id);
        }
        RefreshSelection();
    }

    private void Insert(IReadOnlyList<Markup> source, int step, string description, int page = -1)
    {
        if (!CanAnnotate || source.Count == 0 || _document is null || _layout.PageCount == 0)
        {
            return;
        }
        page = page >= 0 ? page : Math.Clamp(_currentPage, 0, _layout.PageCount - 1);
        // Versatz nach rechts unten, wie die Seite auf dem Bildschirm erscheint.
        var offset = new TextFrame(default, Transform(page).Rotation, 0, 0).ToUser(new PdfPoint(12 * step, 12 * step));
        var scale = ScaleProvider(page);
        var now = DateTimeOffset.Now;
        var copies = source.Select(m => MarkupGeometry.Translate(m, offset.X, offset.Y) with
        {
            Id = Markup.NewId(),
            Author = Author,
            Created = now,
            Modified = now,
            Scale = MarkupKinds.NeedsScale(m.Kind) ? scale ?? m.Scale : m.Scale,
            // Kopien sind neue Markierungen: ohne Status und ohne Sperre.
            Status = null,
            Locked = false,
        }).ToList();
        if (!Run(() => _history!.Apply(page, [.. _document.GetMarkups(page), .. copies], description)))
        {
            return;
        }
        ClearSelection(raise: false);
        _selectedPage = page;
        foreach (var c in copies)
        {
            _selected.Add(c.Id);
        }
        RefreshSelection();
    }

    public void CancelDrawing()
    {
        _drawing?.Cancel();
        _drawing = null;
        if (_drawingPage >= 0 && _visuals.TryGetValue(_drawingPage, out var v))
        {
            v.Layer.SetPreview(null);
        }
        _drawingPage = -1;
    }

    // ---------- Anzeige ----------

    private void OnMarkupsChanged(object? sender, int page)
    {
        if (_visuals.TryGetValue(page, out var visual))
        {
            RefreshLayer(visual);
        }
        PruneSelection();
    }

    private void RefreshLayer(PageVisual visual)
    {
        if (_document is null)
        {
            return;
        }
        visual.Layer.SetMarkups(_document.GetMarkups(visual.Index));
        visual.Layer.SetOverrides(new Dictionary<string, Markup>());
        visual.Layer.SetSelection(visual.Index == _selectedPage ? _selected : new HashSet<string>());
        ApplySearch(visual, visual.Index);
    }

    private void RefreshSelection(bool raise = true)
    {
        foreach (var (index, visual) in _visuals)
        {
            visual.Layer.SetSelection(index == _selectedPage ? new HashSet<string>(_selected) : new HashSet<string>());
        }
        if (raise)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Entfernt Markierungen aus der Auswahl, die es nicht mehr gibt (z. B. nach Rückgängig).</summary>
    private void PruneSelection()
    {
        if (_selected.Count == 0)
        {
            return;
        }
        if (_document is null || _selectedPage >= _layout.PageCount || _selectedPage >= _document.PageCount)
        {
            ClearSelection();
            return;
        }
        var existing = _document.GetMarkups(_selectedPage).Select(m => m.Id).ToHashSet();
        if (_selected.RemoveWhere(id => !existing.Contains(id)) > 0)
        {
            RefreshSelection();
        }
        else
        {
            RefreshSelection(raise: false);
        }
    }

    private void UpdateCursor(MouseEventArgs? e)
    {
        if (_panStart is not null)
        {
            return;
        }
        Cursor = _tool switch
        {
            ViewerTool.Pan => Cursors.Hand,
            ViewerTool.Draw or ViewerTool.Calibrate => CanAnnotate ? Cursors.Cross : Cursors.No,
            _ => e is not null && MarkupAt(e, out _, out _) is not null ? Cursors.SizeAll : Cursors.Arrow,
        };
    }

    // ---------- Treffer ----------

    /// <summary>Seite unter dem Mauszeiger mit Position in der Seite (DIP) und in Benutzerkoordinaten.</summary>
    private (int Page, Point Local, PdfPoint User)? PageAt(MouseEventArgs e, bool clampTo = false, int page = -1)
    {
        if (_document is null || _layout.PageCount == 0)
        {
            return null;
        }
        var pos = e.GetPosition(_canvas);
        int index;
        if (page >= 0)
        {
            index = page;
        }
        else if (_layout.HitTest(pos.X, pos.Y) is { } hit)
        {
            index = hit.Page;
        }
        else
        {
            return null;
        }
        var rect = _layout.PageRect(index);
        var local = new Point(pos.X - rect.X, pos.Y - rect.Y);
        var inside = local.X >= 0 && local.Y >= 0 && local.X <= rect.Width && local.Y <= rect.Height;
        if (!inside && !clampTo)
        {
            return null;
        }
        local = new Point(Math.Clamp(local.X, 0, rect.Width), Math.Clamp(local.Y, 0, rect.Height));
        if (!_visuals.TryGetValue(index, out var visual))
        {
            return null;
        }
        return (index, local, visual.Layer.ToUser(local));
    }

    private Markup? MarkupAt(MouseEventArgs e, out int page, out Point local)
    {
        page = -1;
        local = default;
        if (!_markupsVisible || PageAt(e) is not { } hit || !_visuals.TryGetValue(hit.Page, out var visual))
        {
            return null;
        }
        page = hit.Page;
        local = hit.Local;
        var tolerance = HitTolerance / Math.Max(1e-9, _layout.Zoom * PageLayoutDipPerPoint);
        var current = visual.Layer.Markups.Select(visual.Layer.Current).ToList();
        return MarkupGeometry.HitTestTopmost(current, hit.User, tolerance);
    }

    private const double PageLayoutDipPerPoint = Egle360.Pdf.Viewing.PageLayout.DipPerPoint;

    // ---------- Maus ----------

    private void OnLeftDown(object sender, MouseButtonEventArgs e)
    {
        if (IsOverScrollBar(e))
        {
            return;
        }
        _scroll.Focus();
        e.Handled = true;
        switch (_tool)
        {
            case ViewerTool.Pan:
                StartPan(e);
                break;
            case ViewerTool.Draw or ViewerTool.Calibrate when CanAnnotate:
                DrawDown(e);
                break;
            case ViewerTool.Draw or ViewerTool.Calibrate:
                StartPan(e);
                break;
            default:
                SelectDown(e);
                break;
        }
    }

    private void DrawDown(MouseButtonEventArgs e)
    {
        var kind = EffectiveKind;
        var multi = MarkupKinds.IsMultiPoint(kind);
        var hit = _drawing is { IsActive: true } && multi ? PageAt(e, clampTo: true, page: _drawingPage) : PageAt(e);
        if (hit is not { } h)
        {
            return;
        }
        if (multi && e.ClickCount >= 2 && _drawing is not null)
        {
            Commit(_drawing.Finish(), _drawingPage);
            return;
        }
        if (_drawing is null || !_drawing.IsActive)
        {
            MeasureScale? scale = null;
            if (_tool == ViewerTool.Draw && MarkupKinds.NeedsScale(kind))
            {
                scale = ScaleProvider(h.Page);
                if (scale is null)
                {
                    ScaleRequired?.Invoke(this, h.Page);
                    return;
                }
            }
            var rotation = Transform(h.Page).Rotation;
            var minSize = DragThreshold / (_layout.Zoom * PageLayoutDipPerPoint);
            var preset = _tool == ViewerTool.Draw && ActivePreset?.Kind == kind ? ActivePreset : null;
            var style = _tool == ViewerTool.Calibrate ? CalibrationStyle : preset?.Style ?? StyleProvider(kind);
            var text = _tool != ViewerTool.Draw ? "" : preset is { Text.Length: > 0 } ? preset.Text : TextProvider(kind);
            var image = kind == MarkupKind.Image ? preset?.Image ?? PendingImage : null;
            _drawing = new MarkupDrawing(kind, style, Author, rotation, minSize, scale, text, image);
            _drawingPage = h.Page;
        }
        _drawing.Press(Snap(h.Page, h.User));
        _scroll.CaptureMouse();
        _visuals[_drawingPage].Layer.SetPreview(_drawing.Preview);
    }

    private void SelectDown(MouseButtonEventArgs e)
    {
        if (PageAt(e) is not { } hit || !_visuals.TryGetValue(hit.Page, out var visual))
        {
            ClearSelection();
            StartPan(e);
            return;
        }
        // Zuerst die Griffe der einzeln ausgewählten Markierung.
        if (CanAnnotate && hit.Page == _selectedPage && _selected.Count == 1)
        {
            var selected = visual.Layer.Markups.FirstOrDefault(m => _selected.Contains(m.Id));
            if (selected is { Locked: false } && visual.Layer.HandleAt(selected, hit.Local) is var handle and >= 0)
            {
                _drag = new EditDrag(hit.Page, hit.Local, [selected], handle);
                _scroll.CaptureMouse();
                return;
            }
        }
        var markup = MarkupAt(e, out _, out _);
        if (markup is null)
        {
            ClearSelection();
            StartPan(e);
            return;
        }
        if (e.ClickCount >= 2)
        {
            Select(hit.Page, markup.Id);
            EditTextRequested?.Invoke(this, (hit.Page, markup));
            return;
        }
        var ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
        if (ctrl && hit.Page == _selectedPage)
        {
            if (!_selected.Remove(markup.Id))
            {
                _selected.Add(markup.Id);
            }
            RefreshSelection();
        }
        else if (!(_selected.Contains(markup.Id) && hit.Page == _selectedPage))
        {
            Select(hit.Page, markup.Id);
        }
        if (CanAnnotate && _selected.Count > 0)
        {
            var originals = visual.Layer.Markups.Where(m => _selected.Contains(m.Id) && !m.Locked).ToList();
            if (originals.Count > 0)
            {
                _drag = new EditDrag(hit.Page, hit.Local, originals, -1);
                _scroll.CaptureMouse();
            }
        }
    }

    private void OnMouseMove(object sender, MouseEventArgs e)
    {
        if (PanMove(e))
        {
            return;
        }
        if (_drawing is { IsActive: true } && PageAt(e, clampTo: true, page: _drawingPage) is { } h)
        {
            _drawing.Move(Snap(h.Page, h.User));
            _visuals.GetValueOrDefault(_drawingPage)?.Layer.SetPreview(_drawing.Preview);
            return;
        }
        if (_tool is ViewerTool.Draw or ViewerTool.Calibrate && _drawing is not { IsActive: true })
        {
            // Vor dem ersten Klick zeigt die Fangmarke, wo der Punkt landen würde.
            if (PageAt(e) is { } hover)
            {
                Snap(hover.Page, hover.User);
            }
            else
            {
                ShowSnapMarker(-1, null);
            }
        }
        if (_drag is { } drag && e.LeftButton == MouseButtonState.Pressed && _visuals.TryGetValue(drag.Page, out var visual))
        {
            var pos = PageAt(e, clampTo: true, page: drag.Page)!.Value;
            if (!drag.Moved && (pos.Local - drag.StartLocal).Length < DragThreshold)
            {
                return;
            }
            drag.Moved = true;
            var start = visual.Layer.ToUser(drag.StartLocal);
            var overrides = drag.Handle >= 0
                ? drag.Originals.ToDictionary(m => m.Id, m => MarkupGeometry.DragHandle(m, drag.Handle, SnapHandle(drag.Page, pos.User, m)))
                : drag.Originals.ToDictionary(m => m.Id, m => MarkupGeometry.Translate(m, pos.User.X - start.X, pos.User.Y - start.Y));
            visual.Layer.SetOverrides(overrides);
            return;
        }
        UpdateCursor(e);
    }

    private void OnLeftUp(object sender, MouseButtonEventArgs e)
    {
        if (EndPan())
        {
            UpdateCursor(e);
            return;
        }
        if (_drawing is { IsActive: true } drawing && PageAt(e, clampTo: true, page: _drawingPage) is { } h)
        {
            var page = _drawingPage;
            var markup = drawing.Release(Snap(h.Page, h.User));
            ShowSnapMarker(-1, null);
            // Erst nach dem Auswerten freigeben: Der Verlust der Mauserfassung bricht sonst das Zeichnen ab.
            if (!MarkupKinds.IsMultiPoint(drawing.Kind) || markup is not null)
            {
                _scroll.ReleaseMouseCapture();
            }
            if (markup is not null)
            {
                Commit(markup, page);
            }
            else
            {
                _scroll.ReleaseMouseCapture();
                _visuals.GetValueOrDefault(page)?.Layer.SetPreview(drawing.Preview);
                if (!drawing.IsActive)
                {
                    CancelDrawing();
                }
            }
            return;
        }
        if (_drag is { } drag)
        {
            _drag = null;
            ShowSnapMarker(-1, null);
            _scroll.ReleaseMouseCapture();
            if (drag.Moved && _visuals.TryGetValue(drag.Page, out var visual))
            {
                var changed = drag.Originals.Select(visual.Layer.Current).ToList();
                var description = drag.Handle >= 0 ? $"{MarkupKinds.DisplayName(changed[0].Kind)} ändern"
                    : changed.Count == 1 ? $"{MarkupKinds.DisplayName(changed[0].Kind)} verschieben" : $"{changed.Count} Markierungen verschieben";
                Run(() => _history!.ReplaceMany(drag.Page, changed, description));
                visual.Layer.SetOverrides(new Dictionary<string, Markup>());
            }
        }
    }

    /// <summary>
    /// Mit gedrückter Umschalttaste rastet der Punkt ein (waagrecht, senkrecht, 45 Grad, Quadrat). Sonst fängt der
    /// Objektfang ihn an Linienenden und -mitten des Plans oder an Eckpunkten anderer Markierungen, wenn er nahe genug ist.
    /// </summary>
    private PdfPoint Snap(int page, PdfPoint p)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            ShowSnapMarker(-1, null);
            return _drawing?.Anchor is { } anchor ? MarkupDrawing.Constrain(_drawing.Kind, anchor, p) : p;
        }
        var snapped = FindSnap(page, p, null);
        ShowSnapMarker(page, snapped);
        return snapped ?? p;
    }

    /// <summary>Objektfang beim Ziehen eines Eckpunkts; die bearbeitete Markierung fängt sich nicht selbst.</summary>
    private PdfPoint SnapHandle(int page, PdfPoint p, Markup editing)
    {
        if (!MarkupGeometry.UsesVertexHandles(editing.Kind) && editing.Kind != MarkupKind.Callout || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return p;
        }
        var snapped = FindSnap(page, p, editing.Id);
        ShowSnapMarker(page, snapped);
        return snapped ?? p;
    }

    private PdfPoint? FindSnap(int page, PdfPoint p, string? excludeId)
    {
        if (!SnapEnabled || _document is null || page < 0 || page >= _layout.PageCount)
        {
            return null;
        }
        var tolerance = SnapTolerance / (_layout.Zoom * PageLayoutDipPerPoint);
        if (!_snapIndexes.TryGetValue(page, out var content))
        {
            try
            {
                content = _document.GetSnapIndex(page);
            }
            catch (PdfException)
            {
                content = SnapIndex.Empty;
            }
            _snapIndexes[page] = content;
        }
        PdfPoint? best = content.Nearest(p, tolerance);
        var bestDistance = best is { } b ? b.DistanceTo(p) : tolerance;
        foreach (var m in _document.GetMarkups(page))
        {
            if (m.Id == excludeId || m.Kind is MarkupKind.Freehand or MarkupKind.Note)
            {
                continue;
            }
            foreach (var q in m.Paths.SelectMany(path => path))
            {
                var d = q.DistanceTo(p);
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = q;
                }
            }
        }
        return best;
    }

    private int _snapMarkerPage = -1;

    private void ShowSnapMarker(int page, PdfPoint? point)
    {
        if (_snapMarkerPage >= 0 && _snapMarkerPage != page)
        {
            _visuals.GetValueOrDefault(_snapMarkerPage)?.Layer.SetSnapMarker(null);
        }
        _snapMarkerPage = point is null ? -1 : page;
        if (page >= 0)
        {
            _visuals.GetValueOrDefault(page)?.Layer.SetSnapMarker(point);
        }
    }

    private void Commit(Markup? markup, int page)
    {
        CancelDrawing();
        _scroll.ReleaseMouseCapture();
        if (markup is null || _history is null)
        {
            return;
        }
        if (_tool == ViewerTool.Calibrate)
        {
            SetTool(ViewerTool.Select);
            CalibrationMeasured?.Invoke(this, (page, markup.Points[0].DistanceTo(markup.Points[1])));
            return;
        }
        if (_cutout is { } cut)
        {
            SetTool(ViewerTool.Select);
            var area = _document?.GetMarkups(cut.Page).FirstOrDefault(m => m.Id == cut.Id);
            if (area is not null && cut.Page == page)
            {
                Run(() => _history.Replace(page, area.WithPaths([.. area.Paths, markup.Points]), "Ausschnitt hinzufügen"));
                Select(page, area.Id);
            }
            return;
        }
        if (MarkupKinds.IsTextMarkup(markup.Kind) && _document is not null)
        {
            // Hervorheben, Unterstreichen und Durchstreichen folgen dem Text im aufgezogenen Bereich. Ohne Text
            // bleibt die Hervorhebung ein Rechteck; Unter- und Durchstreichen ohne Text ergeben nichts.
            var rects = _document.GetTextRects(page, PdfRect.FromPoints(markup.Points[0], markup.Points[1]));
            if (rects.Count > 0)
            {
                markup = markup.WithPaths([.. rects.Select(r => (IReadOnlyList<PdfPoint>)[new PdfPoint(r.Left, r.Bottom), new PdfPoint(r.Right, r.Top)])]);
            }
            else if (markup.Kind != MarkupKind.Highlight)
            {
                SetTool(ViewerTool.Select);
                return;
            }
        }
        if (ActivePreset is { } preset)
        {
            markup = preset.ApplyTo(markup);
        }
        if (!Run(() => _history.Add(page, markup)))
        {
            return;
        }
        if (ActivePreset is not null)
        {
            // Werkzeuge aus der Bibliothek bleiben aktiv; Text fragen sie nur ab, wenn das Werkzeug keinen vorgibt.
            if (MarkupKinds.HasText(markup.Kind) && string.IsNullOrEmpty(markup.Text))
            {
                EditTextRequested?.Invoke(this, (page, markup));
            }
            return;
        }
        // Nach dem Zeichnen zurück zur Auswahl, die neue Markierung ist ausgewählt.
        SetTool(ViewerTool.Select);
        Select(page, markup.Id);
        // Neue Textfelder, Sprechblasen und Notizen fragen gleich nach dem Text; Stempel haben ihre Aufschrift schon.
        if (MarkupKinds.HasText(markup.Kind) && string.IsNullOrEmpty(markup.Text))
        {
            EditTextRequested?.Invoke(this, (page, markup));
        }
    }

    /// <summary>Bricht Ziehen ab, wenn die Maus unerwartet freigegeben wird (z. B. durch einen Dialog).</summary>
    private void AbortPointerAction()
    {
        _panStart = null;
        if (_drag is { } drag)
        {
            _drag = null;
            _visuals.GetValueOrDefault(drag.Page)?.Layer.SetOverrides(new Dictionary<string, Markup>());
        }
        if (_drawing is { } d && !MarkupKinds.IsMultiPoint(d.Kind))
        {
            CancelDrawing();
        }
    }

    private void OnRightUp(object sender, MouseButtonEventArgs e)
    {
        if (_drawing is { IsActive: true })
        {
            // Rechtsklick schließt Polylinie, Polygon und Wolke ab, wie die Eingabetaste.
            Commit(_drawing.Finish(), _drawingPage);
            e.Handled = true;
            return;
        }
        var markup = MarkupAt(e, out var page, out _);
        if (markup is null)
        {
            return;
        }
        if (!_selected.Contains(markup.Id) || page != _selectedPage)
        {
            Select(page, markup.Id);
        }
        var menu = new ContextMenu { PlacementTarget = _scroll };
        var editable = CanAnnotate && !markup.Locked;
        MenuItem Add(string header, Action action, bool enabled = true, ItemsControl? parent = null)
        {
            var item = new MenuItem { Header = header, IsEnabled = enabled };
            item.Click += (_, _) => action();
            (parent ?? menu).Items.Add(item);
            return item;
        }
        Add(MarkupKinds.HasText(markup.Kind) ? "Text bearbeiten …" : "Kommentar bearbeiten …", () => EditTextRequested?.Invoke(this, (page, markup)), editable);
        menu.Items.Add(new Separator());
        if (Measurement.SupportsDepth(markup.Kind))
        {
            var depthHeader = markup.Kind == MarkupKind.Area ? "Tiefe für Volumen …" : "Höhe für Wandfläche …";
            Add(depthHeader, () => DepthRequested?.Invoke(this, (page, markup)), editable && markup.Scale is not null);
            if (Measurement.SupportsSlope(markup.Kind))
            {
                Add(markup.Slope is { } slope ? $"Neigung ({Measurement.SlopeText(slope)}) …" : "Neigung oder Gefälle …",
                    () => SlopeRequested?.Invoke(this, (page, markup)), editable && markup.Scale is not null);
            }
            if (markup.Kind != MarkupKind.Area)
            {
                menu.Items.Add(new Separator());
            }
        }
        if (markup.Kind == MarkupKind.Area)
        {
            Add("Ausschnitt hinzufügen", () => StartCutout(page, markup.Id), editable);
            if (markup.Paths.Count > 1)
            {
                Add("Ausschnitte entfernen", () => Run(() => _history!.Replace(page, markup.WithPaths([markup.Points]), "Ausschnitte entfernen")), editable);
            }
            menu.Items.Add(new Separator());
        }
        if (MarkupKinds.IsSummable(markup.Kind))
        {
            Add(markup.UnitPrice is { } price ? $"Einheitspreis ({Measurement.Number(price, 2)} €) …" : "Einheitspreis …",
                () => PriceRequested?.Invoke(this, (page, markup)), editable);
            menu.Items.Add(new Separator());
        }
        var status = new MenuItem { Header = markup.Status is { } st ? $"Status: {st.Name}" : "Status", IsEnabled = CanAnnotate };
        foreach (var name in MarkupStatus.Names)
        {
            var item = Add(name, () => ApplyToSelection(m => m.WithStatus(new MarkupStatus(name, Author, DateTimeOffset.Now)), $"Status „{name}“", includeLocked: true),
                parent: status);
            item.IsCheckable = true;
            item.IsChecked = markup.Status?.Name == name;
        }
        status.Items.Add(new Separator());
        Add("Kein Status", () => ApplyToSelection(m => m.WithStatus(null), "Status entfernen", includeLocked: true), markup.Status is not null, status);
        menu.Items.Add(status);
        Add(markup.Locked ? "Entsperren" : "Sperren", () => ApplyToSelection(m => m with { Locked = !markup.Locked, Modified = DateTimeOffset.Now },
            markup.Locked ? "Entsperren" : "Sperren", includeLocked: true), CanAnnotate);
        menu.Items.Add(new Separator());
        if (_selected.Count >= 2)
        {
            var align = new MenuItem { Header = "Ausrichten", IsEnabled = CanAnnotate };
            foreach (var a in Enum.GetValues<MarkupAlignment>())
            {
                if (a == MarkupAlignment.DistributeHorizontally)
                {
                    align.Items.Add(new Separator());
                }
                Add(MarkupArrange.DisplayName(a), () => AlignSelection(a),
                    a is not (MarkupAlignment.DistributeHorizontally or MarkupAlignment.DistributeVertically) || _selected.Count >= 3, align);
            }
            menu.Items.Add(align);
            menu.Items.Add(new Separator());
        }
        var rotatable = SelectedMarkups.Where(m => !m.Locked && MarkupRotation.CanRotate(m.Kind)).ToList();
        var rotate = new MenuItem { Header = "Drehen", IsEnabled = CanAnnotate && rotatable.Count > 0 };
        Add("90° im Uhrzeigersinn", () => RotateSelection(1), parent: rotate);
        Add("90° gegen den Uhrzeigersinn", () => RotateSelection(3), parent: rotate);
        Add("180°", () => RotateSelection(2), parent: rotate);
        rotate.Items.Add(new Separator());
        Add("Um Winkel drehen …", () => RotateAngleRequested?.Invoke(this, EventArgs.Empty),
            rotatable.Count > 0 && rotatable.All(m => MarkupRotation.CanRotateFreely(m.Kind)), rotate);
        menu.Items.Add(rotate);
        menu.Items.Add(new Separator());
        Add("Als Werkzeug speichern …", () => SaveAsToolRequested?.Invoke(this, markup));
        Add("Darstellung kopieren (Strg+Umschalt+C)", CopyStyle);
        Add("Darstellung einfügen (Strg+Umschalt+V)", PasteStyle, CanAnnotate && HasStyleClipboard);
        menu.Items.Add(new Separator());
        Add("Kopieren (Strg+C)", CopySelection);
        Add("Duplizieren (Strg+D)", DuplicateSelection, editable);
        menu.Items.Add(new Separator());
        Add("In den Vordergrund", () => Reorder(page, markup.Id, toFront: true), editable);
        Add("In den Hintergrund", () => Reorder(page, markup.Id, toFront: false), editable);
        menu.Items.Add(new Separator());
        Add("Löschen", DeleteSelection, editable);
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void Reorder(int page, string id, bool toFront)
    {
        var list = _document!.GetMarkups(page).ToList();
        var m = list.First(x => x.Id == id);
        list.Remove(m);
        if (toFront)
        {
            list.Add(m);
        }
        else
        {
            list.Insert(0, m);
        }
        Run(() => _history!.Apply(page, list, toFront ? "In den Vordergrund" : "In den Hintergrund"));
    }

    private bool HandleMarkupKey(Key key)
    {
        switch (key)
        {
            case Key.Escape when _drawing is { IsActive: true }:
                CancelDrawing();
                return true;
            case Key.Escape when _selected.Count > 0:
                ClearSelection();
                return true;
            case Key.Escape when _tool != ViewerTool.Select:
                SetTool(ViewerTool.Select);
                return true;
            case Key.Enter when _drawing is { IsActive: true }:
                Commit(_drawing.Finish(), _drawingPage);
                return true;
            case Key.Delete or Key.Back when _selected.Count > 0:
                DeleteSelection();
                return true;
            default:
                return false;
        }
    }

    /// <summary>Führt eine Änderung aus und zeigt Fehler (z. B. fehlende Berechtigung) als Meldung.</summary>
    private bool Run(Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (PdfException ex)
        {
            MessageBox.Show(Window.GetWindow(this)!, ex.Message, "EGLE360 PLAN", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
    }
}
