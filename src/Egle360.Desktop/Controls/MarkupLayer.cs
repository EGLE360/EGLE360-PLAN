using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using Egle360.Pdf;
using Egle360.Pdf.Markups;
using Egle360.Pdf.Viewing;

namespace Egle360.Desktop.Controls;

/// <summary>
/// Zeichnet die Markierungen einer Seite als Vektorgrafik über dem Seitenbild, dazu Auswahl, Griffe und die
/// Vorschau beim Zeichnen. Die Geometrie stammt aus <see cref="MarkupGeometry"/>, also derselben Quelle wie der
/// PDF-Erscheinungsstrom.
/// </summary>
public sealed class MarkupLayer : FrameworkElement
{
    /// <summary>Kantenlänge der Griffe in DIP.</summary>
    public const double HandleSize = 8;

    private static readonly ConditionalWeakTable<Markup, ShapeVisual> Cache = new();

    private IReadOnlyList<Markup> _markups = [];
    private IReadOnlyDictionary<string, Markup> _overrides = new Dictionary<string, Markup>();
    private IReadOnlySet<string> _selected = new HashSet<string>();
    private Markup? _preview;
    private IReadOnlyList<PdfRect> _searchHits = [];
    private IReadOnlyList<PdfRect> _currentHit = [];
    private PageTransform _transform;
    private double _scale = 1;

    public MarkupLayer(ITextOutliner outliner)
    {
        Outliner = outliner;
        IsHitTestVisible = false;
    }

    public ITextOutliner Outliner { get; }

    /// <summary>Umrechnung Benutzerkoordinaten in DIP innerhalb der Seite.</summary>
    public Matrix UserToLocal { get; private set; } = Matrix.Identity;

    public void SetPage(PageTransform transform, double dipPerPoint)
    {
        _transform = transform;
        _scale = dipPerPoint;
        var m = transform.Matrix;
        UserToLocal = new Matrix(m.M11 * _scale, m.M12 * _scale, m.M21 * _scale, m.M22 * _scale, m.OffsetX * _scale, m.OffsetY * _scale);
        InvalidateVisual();
    }

    public void SetMarkups(IReadOnlyList<Markup> markups)
    {
        _markups = markups;
        InvalidateVisual();
    }

    /// <summary>Vorläufige Fassungen während des Verschiebens oder Ziehens an Griffen.</summary>
    public void SetOverrides(IReadOnlyDictionary<string, Markup> overrides)
    {
        _overrides = overrides;
        InvalidateVisual();
    }

    public void SetSelection(IReadOnlySet<string> selected)
    {
        _selected = selected;
        InvalidateVisual();
    }

    private PdfPoint? _snapMarker;

    /// <summary>Fangmarke: kleines Quadrat an dem Punkt, an dem der nächste Klick einrastet.</summary>
    public void SetSnapMarker(PdfPoint? point)
    {
        if (_snapMarker == point)
        {
            return;
        }
        _snapMarker = point;
        InvalidateVisual();
    }

    public void SetPreview(Markup? preview)
    {
        _preview = preview;
        InvalidateVisual();
    }

    /// <summary>Fundstellen der Textsuche auf dieser Seite; die aktuelle wird kräftiger hervorgehoben.</summary>
    public void SetSearchHits(IReadOnlyList<PdfRect> hits, IReadOnlyList<PdfRect> current)
    {
        if (hits.Count == 0 && current.Count == 0 && _searchHits.Count == 0 && _currentHit.Count == 0)
        {
            return;
        }
        _searchHits = hits;
        _currentHit = current;
        InvalidateVisual();
    }

    public IReadOnlyList<Markup> Markups => _markups;

    /// <summary>Aktuelle Fassung einer Markierung, einschließlich vorläufiger Änderungen.</summary>
    public Markup Current(Markup m) => _overrides.GetValueOrDefault(m.Id, m);

    protected override void OnRender(DrawingContext dc)
    {
        dc.PushClip(new RectangleGeometry(new Rect(0, 0, ActualWidth, ActualHeight)));
        dc.PushTransform(new MatrixTransform(UserToLocal));
        DrawSearchHits(dc);
        foreach (var m in _markups)
        {
            Draw(dc, Current(m));
        }
        if (_preview is not null)
        {
            Draw(dc, _preview);
        }
        dc.Pop();
        foreach (var m in _markups.Where(m => _selected.Contains(m.Id)))
        {
            DrawSelection(dc, Current(m));
        }
        if (_snapMarker is { } snap)
        {
            var c = ToLocal(snap);
            dc.DrawRectangle(null, SnapPen, new Rect(c.X - 5, c.Y - 5, 10, 10));
        }
        dc.Pop();
    }

    private static readonly Pen SnapPen = FrozenPen(new Pen(new SolidColorBrush(Color.FromRgb(30, 140, 60)), 2));

    private static Pen FrozenPen(Pen p)
    {
        p.Freeze();
        return p;
    }

    private static readonly Brush HitBrush = Frozen(new SolidColorBrush(Color.FromArgb(90, 255, 214, 0)));
    private static readonly Brush CurrentHitBrush = Frozen(new SolidColorBrush(Color.FromArgb(120, 255, 140, 0)));

    private static Brush Frozen(Brush b)
    {
        b.Freeze();
        return b;
    }

    private void DrawSearchHits(DrawingContext dc)
    {
        foreach (var r in _searchHits)
        {
            dc.DrawRectangle(HitBrush, null, new Rect(r.Left, r.Bottom, r.Width, r.Height));
        }
        if (_currentHit.Count > 0)
        {
            var pen = new Pen((Brush)FindResource("AccentBrush"), 1.5 / Math.Max(_scale, 0.01));
            foreach (var r in _currentHit)
            {
                var box = new Rect(r.Left, r.Bottom, r.Width, r.Height);
                box.Inflate(1, 1);
                dc.DrawRectangle(CurrentHitBrush, pen, box);
            }
        }
    }

    private void Draw(DrawingContext dc, Markup m)
    {
        var visual = Cache.GetValue(m, key => new ShapeVisual(MarkupGeometry.BuildShape(key, Outliner)));
        if (visual.Shape.Opacity < 1)
        {
            dc.PushOpacity(visual.Shape.Opacity);
        }
        foreach (var (geometry, pen, brush, clip) in visual.Layers)
        {
            if (clip is not null)
            {
                dc.PushClip(clip);
            }
            dc.DrawGeometry(brush, pen, geometry);
            if (clip is not null)
            {
                dc.Pop();
            }
        }
        if (visual.Shape.Opacity < 1)
        {
            dc.Pop();
        }
    }

    private void DrawSelection(DrawingContext dc, Markup m)
    {
        var accent = (Brush)FindResource("AccentBrush");
        var outline = new Pen(accent, 1) { DashStyle = new DashStyle([4, 3], 0) };
        var r = MarkupGeometry.HandleRect(m);
        var corners = new[] { new PdfPoint(r.Left, r.Bottom), new PdfPoint(r.Right, r.Top) }.Select(ToLocal).ToArray();
        var box = new Rect(corners[0], corners[1]);
        box.Inflate(3, 3);
        dc.DrawRectangle(null, outline, box);
        var handlePen = new Pen(accent, 1);
        foreach (var h in MarkupGeometry.Handles(m).Select(ToLocal))
        {
            dc.DrawRectangle(Brushes.White, handlePen, new Rect(h.X - HandleSize / 2, h.Y - HandleSize / 2, HandleSize, HandleSize));
        }
    }

    public Point ToLocal(PdfPoint p) => UserToLocal.Transform(new Point(p.X, p.Y));

    public PdfPoint ToUser(Point local)
    {
        var inverse = UserToLocal;
        inverse.Invert();
        var p = inverse.Transform(local);
        return new PdfPoint(p.X, p.Y);
    }

    /// <summary>Griff unter dem Punkt (DIP in der Seite) bei der ausgewählten Markierung, sonst -1.</summary>
    public int HandleAt(Markup m, Point local)
    {
        var handles = MarkupGeometry.Handles(Current(m));
        for (var i = 0; i < handles.Count; i++)
        {
            var h = ToLocal(handles[i]);
            if (Math.Abs(h.X - local.X) <= HandleSize && Math.Abs(h.Y - local.Y) <= HandleSize)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>Vorbereitete WPF-Geometrie einer Markierung, einmal je unveränderlicher Markierung erzeugt.</summary>
    private sealed class ShapeVisual
    {
        public ShapeVisual(MarkupShape shape)
        {
            Shape = shape;
            foreach (var layer in shape.Layers)
            {
                var geometry = ToGeometry(layer.Path, layer.EvenOdd);
                Pen? pen = layer.Stroke is { } s
                    ? new Pen(Brush(s), layer.StrokeWidth) { LineJoin = PenLineJoin.Round, StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round }
                    : null;
                pen?.Freeze();
                var brush = layer.Fill is { } f ? Brush(f) : null;
                Geometry? clip = layer.Clip is { } c ? new RectangleGeometry(new Rect(c.Left, c.Bottom, c.Width, c.Height)) : null;
                clip?.Freeze();
                Layers.Add((geometry, pen, brush, clip));
            }
        }

        public MarkupShape Shape { get; }

        public List<(Geometry Geometry, Pen? Pen, Brush? Brush, Geometry? Clip)> Layers { get; } = [];

        private static Brush Brush(PdfColor c)
        {
            var b = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            b.Freeze();
            return b;
        }

        private static Geometry ToGeometry(PathData path, bool evenOdd)
        {
            var g = new StreamGeometry { FillRule = evenOdd ? FillRule.EvenOdd : FillRule.Nonzero };
            using (var ctx = g.Open())
            {
                foreach (var f in path.Figures)
                {
                    ctx.BeginFigure(new Point(f.Start.X, f.Start.Y), isFilled: true, isClosed: f.Closed);
                    foreach (var s in f.Segments)
                    {
                        if (s.Kind == SegmentKind.Line)
                        {
                            ctx.LineTo(new Point(s.End.X, s.End.Y), isStroked: true, isSmoothJoin: true);
                        }
                        else
                        {
                            ctx.BezierTo(new Point(s.C1.X, s.C1.Y), new Point(s.C2.X, s.C2.Y), new Point(s.End.X, s.End.Y), isStroked: true, isSmoothJoin: true);
                        }
                    }
                }
            }
            g.Freeze();
            return g;
        }
    }
}
