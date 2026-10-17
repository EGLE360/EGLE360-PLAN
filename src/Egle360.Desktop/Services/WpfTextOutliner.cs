using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Services;

/// <summary>
/// Setzt Markierungstext mit einer echten Schrift in Umrisse um. Die Umrisse landen im PDF-Erscheinungsstrom, damit
/// der Text in jedem Programm gleich aussieht, auch wenn die Schrift dort fehlt. Muss im Oberflächen-Thread laufen.
/// </summary>
public sealed class WpfTextOutliner : ITextOutliner
{
    /// <summary>Schrift für Markierungstexte; Arial ist auf Windows immer vorhanden und auf Plänen üblich.</summary>
    private static readonly Typeface Face = new(new FontFamily("Arial"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private readonly Dictionary<(string, double, double), TextOutline> _cache = new();

    public TextOutline Layout(string text, double fontSize, double maxWidth)
    {
        var key = (text, fontSize, Math.Round(maxWidth, 2));
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("de-DE"), FlowDirection.LeftToRight, Face, fontSize, Brushes.Black, 1.0)
        {
            MaxTextWidth = Math.Max(1, maxWidth),
            Trimming = TextTrimming.None,
        };
        var geometry = PathGeometry.CreateFromGeometry(formatted.BuildGeometry(new Point(0, 0)));
        var outline = new TextOutline(Convert(geometry), formatted.WidthIncludingTrailingWhitespace, formatted.Height);
        if (_cache.Count > 500)
        {
            _cache.Clear();
        }
        _cache[key] = outline;
        return outline;
    }

    private static PathData Convert(PathGeometry geometry)
    {
        var path = new PathData();
        foreach (var figure in geometry.Figures)
        {
            var current = figure.StartPoint;
            path.MoveTo(P(current));
            foreach (var segment in figure.Segments)
            {
                switch (segment)
                {
                    case LineSegment l:
                        path.LineTo(P(l.Point));
                        current = l.Point;
                        break;
                    case PolyLineSegment pl:
                        foreach (var p in pl.Points)
                        {
                            path.LineTo(P(p));
                            current = p;
                        }
                        break;
                    case BezierSegment b:
                        path.CurveTo(P(b.Point1), P(b.Point2), P(b.Point3));
                        current = b.Point3;
                        break;
                    case PolyBezierSegment pb:
                        for (var i = 0; i + 2 < pb.Points.Count; i += 3)
                        {
                            path.CurveTo(P(pb.Points[i]), P(pb.Points[i + 1]), P(pb.Points[i + 2]));
                            current = pb.Points[i + 2];
                        }
                        break;
                    case QuadraticBezierSegment q:
                        current = Quadratic(path, current, q.Point1, q.Point2);
                        break;
                    case PolyQuadraticBezierSegment pq:
                        for (var i = 0; i + 1 < pq.Points.Count; i += 2)
                        {
                            current = Quadratic(path, current, pq.Points[i], pq.Points[i + 1]);
                        }
                        break;
                    default:
                        // Bögen kommen in Schriftumrissen nicht vor; sicherheitshalber als Gerade.
                        var flat = segment.GetType().GetProperty("Point")?.GetValue(segment) is Point end ? end : current;
                        path.LineTo(P(flat));
                        current = flat;
                        break;
                }
            }
            if (figure.IsClosed)
            {
                path.Close();
            }
        }
        return path;
    }

    /// <summary>Quadratische Kurve als kubische (exakt).</summary>
    private static Point Quadratic(PathData path, Point start, Point control, Point end)
    {
        var c1 = start + (control - start) * (2.0 / 3.0);
        var c2 = end + (control - end) * (2.0 / 3.0);
        path.CurveTo(P(c1), P(c2), P(end));
        return end;
    }

    private static PdfPoint P(Point p) => new(p.X, p.Y);
}
