namespace Egle360.Pdf.Viewing;

/// <summary>Rechteck in geräteunabhängigen Einheiten (DIP, 1/96 Zoll) oder in Punkt, je nach Verwendung.</summary>
public readonly record struct LayoutRect(double X, double Y, double Width, double Height)
{
    public double Right => X + Width;

    public double Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public LayoutRect Intersect(LayoutRect other)
    {
        var x = Math.Max(X, other.X);
        var y = Math.Max(Y, other.Y);
        var r = Math.Min(Right, other.Right);
        var b = Math.Min(Bottom, other.Bottom);
        return r > x && b > y ? new LayoutRect(x, y, r - x, b - y) : default;
    }
}

/// <summary>
/// Anordnung der Seiten in der fortlaufenden Ansicht (untereinander, zentriert) und alle Zoomberechnungen.
/// Reine Rechenlogik ohne Oberfläche, damit sie unabhängig getestet werden kann.
/// </summary>
/// <remarks>
/// Zoom 1,0 (100 %) bedeutet Originalgröße: 1 Punkt = 1/72 Zoll = 96/72 DIP.
/// </remarks>
public sealed class PageLayout
{
    public const double DipPerPoint = 96.0 / 72.0;
    public const double MinZoom = 0.02;
    public const double MaxZoom = 64.0;

    /// <summary>Zoomstufen für die Schaltflächen und Tastenkürzel.</summary>
    public static readonly double[] ZoomSteps =
    [
        0.02, 0.03, 0.05, 0.0625, 0.08, 0.10, 0.125, 0.15, 0.20, 0.25, 0.33, 0.50, 0.67, 0.75, 1.0, 1.25, 1.5, 2.0,
        3.0, 4.0, 6.0, 8.0, 12.0, 16.0, 24.0, 32.0, 48.0, 64.0,
    ];

    private readonly List<(double WidthPt, double HeightPt)> _pages = [];
    private LayoutRect[] _rects = [];

    public PageLayout(double gap = 16, double margin = 24)
    {
        Gap = gap;
        Margin = margin;
    }

    public double Gap { get; }

    public double Margin { get; }

    public double Zoom { get; private set; } = 1.0;

    public int PageCount => _pages.Count;

    public double ExtentWidth { get; private set; }

    public double ExtentHeight { get; private set; }

    public void SetPages(IEnumerable<(double WidthPt, double HeightPt)> pages)
    {
        _pages.Clear();
        _pages.AddRange(pages);
        Recalculate();
    }

    /// <summary>Breite des sichtbaren Bereichs. Schmalere Seiten werden darin zentriert.</summary>
    public double ViewportWidth { get; private set; }

    public void SetViewportWidth(double width)
    {
        if (Math.Abs(width - ViewportWidth) > 0.5)
        {
            ViewportWidth = Math.Max(0, width);
            Recalculate();
        }
    }

    public void SetZoom(double zoom)
    {
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        Recalculate();
    }

    /// <summary>Lage der Seite im Gesamtbereich in DIP.</summary>
    public LayoutRect PageRect(int index) => _rects[index];

    /// <summary>Seitengröße in Punkt.</summary>
    public (double WidthPt, double HeightPt) PageSize(int index) => _pages[index];

    /// <summary>Seiten, die den sichtbaren Bereich schneiden, in Reihenfolge.</summary>
    public IReadOnlyList<int> VisiblePages(LayoutRect viewport)
    {
        var result = new List<int>();
        if (_rects.Length == 0)
        {
            return result;
        }
        // Binäre Suche nach der ersten Seite, deren Unterkante unter dem oberen Rand liegt.
        int lo = 0, hi = _rects.Length - 1;
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (_rects[mid].Bottom < viewport.Y)
            {
                lo = mid + 1;
            }
            else
            {
                hi = mid;
            }
        }
        for (var i = lo; i < _rects.Length && _rects[i].Y <= viewport.Bottom; i++)
        {
            if (!_rects[i].Intersect(viewport).IsEmpty)
            {
                result.Add(i);
            }
        }
        return result;
    }

    /// <summary>Aktuelle Seite: die Seite, die die Mitte des sichtbaren Bereichs enthält oder ihr am nächsten liegt.</summary>
    public int CurrentPage(LayoutRect viewport)
    {
        if (_rects.Length == 0)
        {
            return -1;
        }
        var centerY = viewport.Y + viewport.Height / 2;
        var best = 0;
        var bestDistance = double.MaxValue;
        foreach (var i in VisiblePagesOrNearest(viewport))
        {
            var r = _rects[i];
            var distance = centerY < r.Y ? r.Y - centerY : centerY > r.Bottom ? centerY - r.Bottom : 0;
            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Zoom, bei dem die breiteste Seite die Breite des Fensters füllt.</summary>
    public double FitWidthZoom(double viewportWidth)
    {
        if (_pages.Count == 0)
        {
            return 1;
        }
        var widest = _pages.Max(p => p.WidthPt);
        return Math.Clamp((viewportWidth - 2 * Margin) / (widest * DipPerPoint), MinZoom, MaxZoom);
    }

    /// <summary>Zoom, bei dem die angegebene Seite vollständig in das Fenster passt.</summary>
    public double FitPageZoom(int index, double viewportWidth, double viewportHeight)
    {
        if (_pages.Count == 0)
        {
            return 1;
        }
        var (w, h) = _pages[Math.Clamp(index, 0, _pages.Count - 1)];
        var zx = (viewportWidth - 2 * Margin) / (w * DipPerPoint);
        var zy = (viewportHeight - 2 * Gap) / (h * DipPerPoint);
        return Math.Clamp(Math.Min(zx, zy), MinZoom, MaxZoom);
    }

    public static double NextZoomStep(double zoom) =>
        ZoomSteps.FirstOrDefault(z => z > zoom * 1.001, MaxZoom);

    public static double PreviousZoomStep(double zoom) =>
        ZoomSteps.LastOrDefault(z => z < zoom * 0.999, MinZoom);

    /// <summary>Wandelt einen Punkt im Gesamtbereich (DIP) in Seite und Seitenkoordinate in Punkt (oben links) um.</summary>
    public (int Page, double XPt, double YPt)? HitTest(double x, double y)
    {
        for (var i = 0; i < _rects.Length; i++)
        {
            var r = _rects[i];
            if (y <= r.Bottom + Gap / 2 || i == _rects.Length - 1)
            {
                var scale = Zoom * DipPerPoint;
                return (i, (x - r.X) / scale, (y - r.Y) / scale);
            }
        }
        return null;
    }

    /// <summary>Position im Gesamtbereich (DIP) eines Seitenpunkts in Punkt (oben links).</summary>
    public (double X, double Y) ToLayout(int page, double xPt, double yPt)
    {
        var r = _rects[page];
        var scale = Zoom * DipPerPoint;
        return (r.X + xPt * scale, r.Y + yPt * scale);
    }

    /// <summary>
    /// Ändert den Zoom so, dass der Punkt unter <paramref name="anchorInViewport"/> an derselben Stelle im Fenster bleibt.
    /// Liefert den neuen Scroll-Versatz.
    /// </summary>
    public (double OffsetX, double OffsetY) ZoomAround(double newZoom, LayoutRect viewport, (double X, double Y) anchorInViewport)
    {
        var anchorX = viewport.X + anchorInViewport.X;
        var anchorY = viewport.Y + anchorInViewport.Y;
        var hit = HitTest(anchorX, anchorY);
        SetZoom(newZoom);
        if (hit is not { } h)
        {
            return (0, 0);
        }
        var (lx, ly) = ToLayout(h.Page, h.XPt, h.YPt);
        return (Math.Max(0, lx - anchorInViewport.X), Math.Max(0, ly - anchorInViewport.Y));
    }

    private IEnumerable<int> VisiblePagesOrNearest(LayoutRect viewport)
    {
        var visible = VisiblePages(viewport);
        return visible.Count > 0 ? visible : Enumerable.Range(0, _rects.Length);
    }

    private void Recalculate()
    {
        var scale = Zoom * DipPerPoint;
        var maxWidth = _pages.Count == 0 ? 0 : _pages.Max(p => p.WidthPt) * scale;
        ExtentWidth = Math.Max(maxWidth + 2 * Margin, ViewportWidth);
        _rects = new LayoutRect[_pages.Count];
        var y = Gap;
        for (var i = 0; i < _pages.Count; i++)
        {
            var w = _pages[i].WidthPt * scale;
            var h = _pages[i].HeightPt * scale;
            _rects[i] = new LayoutRect((ExtentWidth - w) / 2, y, w, h);
            y += h + Gap;
        }
        ExtentHeight = y;
    }
}
