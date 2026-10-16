using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Egle360.Desktop.Services;
using Egle360.Pdf;
using Egle360.Pdf.Markups;
using Egle360.Pdf.Viewing;

namespace Egle360.Desktop.Controls;

public enum FitMode
{
    None,
    Width,
    Page,
}

/// <summary>Was die linke Maustaste in der Ansicht tut.</summary>
public enum ViewerTool
{
    /// <summary>Markierungen auswählen, verschieben und an Griffen ändern; auf leerer Fläche verschieben.</summary>
    Select,

    /// <summary>Nur die Ansicht verschieben.</summary>
    Pan,

    /// <summary>Neue Markierung der Art <see cref="PdfViewer.DrawKind"/> zeichnen.</summary>
    Draw,

    /// <summary>Zwei Punkte einer bekannten Strecke setzen, um den Maßstab zu kalibrieren.</summary>
    Calibrate,
}

/// <summary>
/// Fortlaufende Seitenansicht mit Zoom, Verschieben und Seitennavigation. Nur sichtbare Seiten (und je eine
/// Bildschirmhöhe davor und danach) werden gerendert. Bei hohem Zoom wird zusätzlich nur der sichtbare Ausschnitt
/// scharf gerendert (siehe <see cref="RenderPlanner"/>). Markierungen zeichnet die Ansicht selbst als Vektorgrafik
/// über dem Seitenbild (siehe <see cref="MarkupLayer"/>); dort werden sie auch gezeichnet und bearbeitet.
/// </summary>
public sealed partial class PdfViewer : Grid
{
    private readonly ScrollViewer _scroll;
    private readonly Canvas _canvas;
    private readonly PageLayout _layout = new();
    private readonly Dictionary<int, PageVisual> _visuals = new();
    private readonly DispatcherTimer _detailTimer;
    private PageRenderService? _renderer;
    private PdfDocument? _document;
    private Point? _panStart;
    private Point _panOffset;
    private int _currentPage = -1;
    private int _busy;
    private readonly Dictionary<int, PageTransform> _transforms = new();

    public PdfViewer()
    {
        SetResourceReference(BackgroundProperty, "CanvasBrush");
        _canvas = new Canvas { ClipToBounds = true };
        _scroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _canvas,
            Focusable = true,
            CanContentScroll = false,
            PanningMode = PanningMode.Both,
        };
        Children.Add(_scroll);

        _detailTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(140) };
        _detailTimer.Tick += (_, _) =>
        {
            _detailTimer.Stop();
            UpdateDetails();
        };

        _scroll.ScrollChanged += (_, e) =>
        {
            if (e.ViewportWidthChange != 0)
            {
                OnViewportWidthChanged();
            }
            UpdateVisuals();
        };
        _scroll.PreviewMouseWheel += OnMouseWheel;
        _scroll.PreviewMouseLeftButtonDown += OnLeftDown;
        _scroll.PreviewMouseMove += OnMouseMove;
        _scroll.PreviewMouseLeftButtonUp += OnLeftUp;
        _scroll.PreviewMouseDown += OnMiddleDown;
        _scroll.PreviewMouseUp += OnMiddleUp;
        _scroll.PreviewMouseRightButtonUp += OnRightUp;
        _scroll.PreviewKeyDown += OnKeyDown;
        _scroll.LostMouseCapture += (_, _) => AbortPointerAction();
        UpdateCursor(null);
        Focusable = false;
        AutomationPropertiesHelper.SetName(_scroll, "Dokumentansicht");
    }

    public event EventHandler? ZoomChanged;

    public event EventHandler? CurrentPageChanged;

    public double Zoom => _layout.Zoom;

    public FitMode FitMode { get; private set; } = FitMode.Width;

    public int PageCount => _layout.PageCount;

    public int CurrentPage => _currentPage;

    /// <summary>True, solange Darstellungsaufträge offen sind (für den Screenshot-Modus).</summary>
    public bool IsBusy => _busy > 0 || _detailTimer.IsEnabled;

    public void Attach(PdfDocument document, PageRenderService renderer, MarkupHistory history)
    {
        _document = document;
        _renderer = renderer;
        _history = history;
        document.MarkupsChanged += OnMarkupsChanged;
        Reload();
    }

    public void Detach()
    {
        if (_document is not null)
        {
            _document.MarkupsChanged -= OnMarkupsChanged;
        }
        CancelDrawing();
        ClearVisuals();
        _document = null;
        _history = null;
    }

    /// <summary>Liest die Seitengrößen neu ein, z. B. nach dem Drehen, Löschen oder Einfügen von Seiten.</summary>
    public void Reload()
    {
        if (_document is null)
        {
            return;
        }
        var keepPage = Math.Max(0, _currentPage);
        CancelDrawing();
        ClearVisuals();
        _transforms.Clear();
        PruneSelection();
        _layout.SetPages(_document.GetPages().Select(p => (p.WidthPt, p.HeightPt)));
        _layout.SetViewportWidth(_scroll.ViewportWidth);
        ApplyFit();
        UpdateExtent();
        _currentPage = -1;
        if (_layout.PageCount > 0)
        {
            GoToPage(Math.Min(keepPage, _layout.PageCount - 1));
        }
        UpdateVisuals();
    }

    public void SetZoom(double zoom, Point? anchorInViewport = null)
    {
        FitMode = FitMode.None;
        ApplyZoom(zoom, anchorInViewport);
    }

    public void ZoomIn() => SetZoom(PageLayout.NextZoomStep(_layout.Zoom), ViewportCenter);

    public void ZoomOut() => SetZoom(PageLayout.PreviousZoomStep(_layout.Zoom), ViewportCenter);

    public void FitWidth()
    {
        FitMode = FitMode.Width;
        ApplyFit();
    }

    public void FitPage()
    {
        FitMode = FitMode.Page;
        ApplyFit();
        GoToPage(Math.Max(0, _currentPage));
    }

    public void GoToPage(int index)
    {
        if (_layout.PageCount == 0)
        {
            return;
        }
        index = Math.Clamp(index, 0, _layout.PageCount - 1);
        RememberView();
        var rect = _layout.PageRect(index);
        _scroll.ScrollToVerticalOffset(Math.Max(0, rect.Y - _layout.Gap));
        UpdateCurrentPage(index);
    }

    public void FocusView() => _scroll.Focus();

    /// <summary>Stellt einen Punkt einer Seite (Benutzerkoordinaten) in die Mitte des Fensters.</summary>
    public void CenterOn(int page, PdfPoint point)
    {
        if (_document is null || page < 0 || page >= _layout.PageCount)
        {
            return;
        }
        RememberView();
        var (u, v) = Transform(page).ToDisplay(point);
        var (x, y) = _layout.ToLayout(page, u, v);
        _scroll.ScrollToHorizontalOffset(Math.Max(0, x - _scroll.ViewportWidth / 2));
        _scroll.ScrollToVerticalOffset(Math.Max(0, y - _scroll.ViewportHeight / 2));
        UpdateCurrentPage(page);
    }

    /// <summary>Springt zur Seite, wenn sie gerade nicht sichtbar ist (z. B. nach Rückgängig auf einer anderen Seite).</summary>
    public void GoToPageIfHidden(int index)
    {
        if (index >= 0 && index < _layout.PageCount && !_layout.VisiblePages(Viewport).Contains(index))
        {
            GoToPage(index);
        }
    }

    private Point ViewportCenter => new(_scroll.ViewportWidth / 2, _scroll.ViewportHeight / 2);

    private LayoutRect Viewport => new(_scroll.HorizontalOffset, _scroll.VerticalOffset, _scroll.ViewportWidth, _scroll.ViewportHeight);

    private double PixelsPerDip => VisualTreeHelper.GetDpi(this).PixelsPerDip;

    private void ApplyFit()
    {
        if (_layout.PageCount == 0 || _scroll.ViewportWidth <= 0)
        {
            return;
        }
        var zoom = FitMode switch
        {
            FitMode.Width => _layout.FitWidthZoom(_scroll.ViewportWidth),
            FitMode.Page => _layout.FitPageZoom(Math.Max(0, _currentPage), _scroll.ViewportWidth, _scroll.ViewportHeight),
            _ => _layout.Zoom,
        };
        ApplyZoom(zoom, null);
    }

    private void ApplyZoom(double zoom, Point? anchor)
    {
        if (_layout.PageCount == 0)
        {
            _layout.SetZoom(zoom);
            ZoomChanged?.Invoke(this, EventArgs.Empty);
            return;
        }
        var page = Math.Max(0, _currentPage);
        var (offsetX, offsetY) = anchor is { } a
            ? _layout.ZoomAround(zoom, Viewport, (a.X, a.Y))
            : (double.NaN, double.NaN);
        if (anchor is null)
        {
            _layout.SetZoom(zoom);
        }
        foreach (var visual in _visuals.Values)
        {
            visual.ClearDetail();
        }
        UpdateExtent();
        _scroll.UpdateLayout();
        if (anchor is null)
        {
            var rect = _layout.PageRect(page);
            offsetX = Math.Max(0, (_layout.ExtentWidth - _scroll.ViewportWidth) / 2);
            offsetY = Math.Max(0, rect.Y - _layout.Gap);
        }
        _scroll.ScrollToHorizontalOffset(offsetX);
        _scroll.ScrollToVerticalOffset(offsetY);
        PositionVisuals();
        UpdateVisuals();
        ZoomChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnViewportWidthChanged()
    {
        _layout.SetViewportWidth(_scroll.ViewportWidth);
        if (FitMode != FitMode.None)
        {
            ApplyFit();
        }
        else
        {
            UpdateExtent();
            PositionVisuals();
        }
    }

    private void UpdateExtent()
    {
        _canvas.Width = _layout.ExtentWidth;
        _canvas.Height = _layout.ExtentHeight;
    }

    // ---------- Sichtbare Seiten ----------

    private void UpdateVisuals()
    {
        if (_document is null || _renderer is null || _layout.PageCount == 0 || _scroll.ViewportHeight <= 0)
        {
            return;
        }
        var viewport = Viewport;
        var prefetch = new LayoutRect(viewport.X, viewport.Y - viewport.Height, viewport.Width, viewport.Height * 3);
        var wanted = _layout.VisiblePages(prefetch).ToHashSet();

        foreach (var index in _visuals.Keys.Where(i => !wanted.Contains(i)).ToList())
        {
            _visuals[index].Remove(_canvas);
            _visuals.Remove(index);
        }

        var visibleNow = _layout.VisiblePages(viewport).ToHashSet();
        var required = RenderPlanner.RequiredScale(_layout.Zoom, PixelsPerDip);
        foreach (var index in wanted)
        {
            if (!_visuals.TryGetValue(index, out var visual))
            {
                visual = new PageVisual(index, _canvas, new MarkupLayer(Outliner));
                _visuals[index] = visual;
                RefreshLayer(visual);
            }
            Place(visual, index);
            var (w, h) = _layout.PageSize(index);
            var baseScale = RenderPlanner.BaseScale(w, h, required);
            if (visual.BaseScale is not { } existing || !RenderPlanner.BaseIsGoodEnough(existing, baseScale))
            {
                if (visual.PendingBaseScale is not { } pending || !RenderPlanner.BaseIsGoodEnough(pending, baseScale))
                {
                    RequestBase(visual, baseScale, visibleNow.Contains(index) ? RenderPriority.View : RenderPriority.Background);
                }
            }
        }

        UpdateCurrentPage(_layout.CurrentPage(viewport));
        _detailTimer.Stop();
        _detailTimer.Start();
    }

    private void PositionVisuals()
    {
        foreach (var (index, visual) in _visuals)
        {
            Place(visual, index);
        }
    }

    private void Place(PageVisual visual, int index)
    {
        var rect = _layout.PageRect(index);
        visual.Position(rect);
        var transform = Transform(index);
        visual.Layer.SetPage(transform, rect.Width / Math.Max(1e-9, transform.DisplayWidth));
    }

    private PageTransform Transform(int index)
    {
        if (!_transforms.TryGetValue(index, out var t))
        {
            t = _document!.GetPageTransform(index);
            _transforms[index] = t;
        }
        return t;
    }

    private async void RequestBase(PageVisual visual, double scale, RenderPriority priority)
    {
        visual.BaseCancel?.Cancel();
        var cts = new CancellationTokenSource();
        visual.BaseCancel = cts;
        visual.PendingBaseScale = scale;
        var (w, h) = _layout.PageSize(visual.Index);
        var region = new PixelRect(0, 0, Math.Max(1, (int)Math.Round(w * scale)), Math.Max(1, (int)Math.Round(h * scale)));
        var document = _document!;
        _busy++;
        try
        {
            var bitmap = await _renderer!.RenderAsync(document, visual.Index, scale, region, priority, cts.Token, hideMarkups: true);
            if (bitmap is not null && !cts.IsCancellationRequested && ReferenceEquals(document, _document) && _visuals.ContainsValue(visual))
            {
                visual.SetBase(bitmap, scale);
            }
        }
        finally
        {
            _busy--;
            if (ReferenceEquals(visual.BaseCancel, cts))
            {
                visual.PendingBaseScale = null;
            }
        }
    }

    private void UpdateDetails()
    {
        if (_document is null || _renderer is null)
        {
            return;
        }
        var viewport = Viewport;
        var required = RenderPlanner.RequiredScale(_layout.Zoom, PixelsPerDip);
        var dipToPt = 1 / (_layout.Zoom * PageLayout.DipPerPoint);
        foreach (var index in _layout.VisiblePages(viewport))
        {
            if (!_visuals.TryGetValue(index, out var visual) || visual.BaseScale is not { } baseScale)
            {
                continue;
            }
            if (!RenderPlanner.NeedsDetail(baseScale, required))
            {
                visual.ClearDetail();
                continue;
            }
            var pageRect = _layout.PageRect(index);
            var visible = pageRect.Intersect(viewport);
            if (visible.IsEmpty)
            {
                continue;
            }
            var visiblePt = new LayoutRect((visible.X - pageRect.X) * dipToPt, (visible.Y - pageRect.Y) * dipToPt, visible.Width * dipToPt, visible.Height * dipToPt);
            if (visual.DetailCovers(visiblePt, required))
            {
                continue;
            }
            var (w, h) = _layout.PageSize(index);
            var full = (W: (int)Math.Round(w * required), H: (int)Math.Round(h * required));
            var region = RenderPlanner.DetailRegion(visiblePt, required, full.W, full.H);
            RequestDetail(visual, required, region);
        }
    }

    private async void RequestDetail(PageVisual visual, double scale, PixelRect region)
    {
        visual.DetailCancel?.Cancel();
        var cts = new CancellationTokenSource();
        visual.DetailCancel = cts;
        var document = _document!;
        _busy++;
        try
        {
            var bitmap = await _renderer!.RenderAsync(document, visual.Index, scale, region, RenderPriority.View, cts.Token, hideMarkups: true);
            if (bitmap is not null && !cts.IsCancellationRequested && ReferenceEquals(document, _document) && _visuals.ContainsValue(visual)
                && Math.Abs(RenderPlanner.RequiredScale(_layout.Zoom, PixelsPerDip) - scale) < 1e-9)
            {
                var regionPt = new LayoutRect(region.X / scale, region.Y / scale, region.Width / scale, region.Height / scale);
                visual.SetDetail(bitmap, scale, regionPt, _layout.PageRect(visual.Index), _layout.Zoom * PageLayout.DipPerPoint);
            }
        }
        finally
        {
            _busy--;
        }
    }

    private void ClearVisuals()
    {
        foreach (var visual in _visuals.Values)
        {
            visual.Remove(_canvas);
        }
        _visuals.Clear();
    }

    private void UpdateCurrentPage(int index)
    {
        if (index != _currentPage && index >= 0)
        {
            _currentPage = index;
            CurrentPageChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    // ---------- Eingaben ----------

    private void OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            var factor = e.Delta > 0 ? 1.2 : 1 / 1.2;
            SetZoom(_layout.Zoom * factor, e.GetPosition(_scroll));
            e.Handled = true;
        }
        else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            _scroll.ScrollToHorizontalOffset(_scroll.HorizontalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private bool IsOverScrollBar(MouseEventArgs e) =>
        e.OriginalSource is DependencyObject d && FindAncestor<System.Windows.Controls.Primitives.ScrollBar>(d) is not null;

    private void StartPan(MouseEventArgs e)
    {
        _panStart = e.GetPosition(_scroll);
        _panOffset = new Point(_scroll.HorizontalOffset, _scroll.VerticalOffset);
        _scroll.CaptureMouse();
        Cursor = Cursors.SizeAll;
    }

    private bool PanMove(MouseEventArgs e)
    {
        if (_panStart is not { } start)
        {
            return false;
        }
        var p = e.GetPosition(_scroll);
        _scroll.ScrollToHorizontalOffset(_panOffset.X - (p.X - start.X));
        _scroll.ScrollToVerticalOffset(_panOffset.Y - (p.Y - start.Y));
        return true;
    }

    private bool EndPan()
    {
        if (_panStart is null)
        {
            return false;
        }
        _panStart = null;
        _scroll.ReleaseMouseCapture();
        return true;
    }

    private void OnMiddleDown(object sender, MouseButtonEventArgs e)
    {
        if (HandleHistoryButton(e))
        {
            e.Handled = true;
            return;
        }
        if (e.ChangedButton == MouseButton.Middle && !IsOverScrollBar(e))
        {
            _scroll.Focus();
            StartPan(e);
            e.Handled = true;
        }
    }

    private void OnMiddleUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && EndPan())
        {
            UpdateCursor(e);
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }
        if (HandleMarkupKey(e.Key))
        {
            e.Handled = true;
            return;
        }
        switch (e.Key)
        {
            case Key.PageDown:
                GoToPage(_currentPage + 1);
                e.Handled = true;
                break;
            case Key.PageUp:
                GoToPage(_currentPage - 1);
                e.Handled = true;
                break;
            case Key.Home:
                GoToPage(0);
                e.Handled = true;
                break;
            case Key.End:
                GoToPage(_layout.PageCount - 1);
                e.Handled = true;
                break;
        }
    }

    private static T? FindAncestor<T>(DependencyObject node) where T : DependencyObject
    {
        for (var n = node; n is not null; n = n is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(n) : LogicalTreeHelper.GetParent(n))
        {
            if (n is T t)
            {
                return t;
            }
        }
        return null;
    }

    /// <summary>Darstellung einer Seite: weißer Rahmen, Grundbild und gegebenenfalls ein scharfer Ausschnitt.</summary>
    private sealed class PageVisual
    {
        private readonly Border _frame;
        private readonly Image _base;
        private Image? _detail;
        private LayoutRect _detailPt;
        private double _detailScale;

        public PageVisual(int index, Canvas canvas, MarkupLayer layer)
        {
            Index = index;
            Layer = layer;
            _frame = new Border { Background = Brushes.White, BorderThickness = new Thickness(1) };
            _frame.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
            _base = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
            RenderOptions.SetBitmapScalingMode(_base, BitmapScalingMode.HighQuality);
            canvas.Children.Add(_frame);
            canvas.Children.Add(_base);
            canvas.Children.Add(Layer);
        }

        public int Index { get; }

        public MarkupLayer Layer { get; }

        public double? BaseScale { get; private set; }

        public double? PendingBaseScale { get; set; }

        public CancellationTokenSource? BaseCancel { get; set; }

        public CancellationTokenSource? DetailCancel { get; set; }

        private LayoutRect _rect;

        public void Position(LayoutRect rect)
        {
            _rect = rect;
            Place(_frame, new LayoutRect(rect.X - 1, rect.Y - 1, rect.Width + 2, rect.Height + 2));
            Place(_base, rect);
            Place(Layer, rect);
            if (_detail is not null)
            {
                var s = rect.Width / Math.Max(1e-9, _pageWidthPt);
                Place(_detail, new LayoutRect(rect.X + _detailPt.X * s, rect.Y + _detailPt.Y * s, _detailPt.Width * s, _detailPt.Height * s));
            }
        }

        private double _pageWidthPt;

        public void SetBase(BitmapSource bitmap, double scale)
        {
            _base.Source = bitmap;
            BaseScale = scale;
        }

        public bool DetailCovers(LayoutRect visiblePt, double scale) =>
            _detail is not null && Math.Abs(_detailScale - scale) < 1e-9
            && visiblePt.X >= _detailPt.X - 0.5 && visiblePt.Y >= _detailPt.Y - 0.5
            && visiblePt.Right <= _detailPt.Right + 0.5 && visiblePt.Bottom <= _detailPt.Bottom + 0.5;

        public void SetDetail(BitmapSource bitmap, double scale, LayoutRect regionPt, LayoutRect pageRect, double dipPerPt)
        {
            if (_detail is null)
            {
                _detail = new Image { Stretch = Stretch.Fill, IsHitTestVisible = false };
                RenderOptions.SetBitmapScalingMode(_detail, BitmapScalingMode.NearestNeighbor);
                // Unter den Markierungen einfügen.
                var children = ((Canvas)_base.Parent).Children;
                children.Insert(children.IndexOf(_base) + 1, _detail);
            }
            _detail.Source = bitmap;
            _detailScale = scale;
            _detailPt = regionPt;
            _pageWidthPt = pageRect.Width / dipPerPt;
            Position(pageRect);
        }

        public void ClearDetail()
        {
            DetailCancel?.Cancel();
            if (_detail is not null)
            {
                ((Canvas)_detail.Parent).Children.Remove(_detail);
                _detail = null;
            }
        }

        public void Remove(Canvas canvas)
        {
            BaseCancel?.Cancel();
            ClearDetail();
            canvas.Children.Remove(_frame);
            canvas.Children.Remove(_base);
            canvas.Children.Remove(Layer);
        }

        private static void Place(FrameworkElement element, LayoutRect rect)
        {
            Canvas.SetLeft(element, rect.X);
            Canvas.SetTop(element, rect.Y);
            element.Width = Math.Max(0, rect.Width);
            element.Height = Math.Max(0, rect.Height);
        }
    }
}
