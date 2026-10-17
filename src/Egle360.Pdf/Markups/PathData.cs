namespace Egle360.Pdf.Markups;

public enum SegmentKind
{
    Line,
    Cubic,
}

/// <summary>Strecke oder kubische Bézierkurve bis <see cref="End"/>.</summary>
public readonly record struct PathSegment(SegmentKind Kind, PdfPoint C1, PdfPoint C2, PdfPoint End);

public sealed class PathFigure(PdfPoint start)
{
    public PdfPoint Start { get; } = start;

    public List<PathSegment> Segments { get; } = [];

    public bool Closed { get; set; }
}

/// <summary>
/// Vektorpfad aus Teilfiguren. Dient gleichzeitig als Quelle für den PDF-Erscheinungsstrom und für die Darstellung
/// in der Oberfläche, damit beide gleich aussehen.
/// </summary>
public sealed class PathData
{
    private PathFigure? _current;

    public List<PathFigure> Figures { get; } = [];

    public bool IsEmpty => Figures.Count == 0;

    private PathFigure Current => _current ?? throw new InvalidOperationException("Der Pfad hat keinen Startpunkt (MoveTo fehlt).");

    public PathData MoveTo(PdfPoint p)
    {
        _current = new PathFigure(p);
        Figures.Add(_current);
        return this;
    }

    public PathData LineTo(PdfPoint p)
    {
        Current.Segments.Add(new PathSegment(SegmentKind.Line, default, default, p));
        return this;
    }

    public PathData CurveTo(PdfPoint c1, PdfPoint c2, PdfPoint end)
    {
        Current.Segments.Add(new PathSegment(SegmentKind.Cubic, c1, c2, end));
        return this;
    }

    public PathData Close()
    {
        Current.Closed = true;
        return this;
    }

    public PathData Polyline(IReadOnlyList<PdfPoint> points, bool closed)
    {
        if (points.Count == 0)
        {
            return this;
        }
        MoveTo(points[0]);
        for (var i = 1; i < points.Count; i++)
        {
            LineTo(points[i]);
        }
        if (closed)
        {
            Close();
        }
        return this;
    }

    public PathData Rectangle(PdfRect r) =>
        MoveTo(new(r.Left, r.Bottom)).LineTo(new(r.Right, r.Bottom)).LineTo(new(r.Right, r.Top)).LineTo(new(r.Left, r.Top)).Close();

    /// <summary>Ellipse aus vier Bézierkurven (Abweichung unter 0,03 % des Radius).</summary>
    public PathData Ellipse(PdfRect r)
    {
        const double k = 0.5522847498;
        var cx = (r.Left + r.Right) / 2;
        var cy = (r.Bottom + r.Top) / 2;
        var rx = r.Width / 2;
        var ry = r.Height / 2;
        MoveTo(new(cx + rx, cy));
        CurveTo(new(cx + rx, cy + ry * k), new(cx + rx * k, cy + ry), new(cx, cy + ry));
        CurveTo(new(cx - rx * k, cy + ry), new(cx - rx, cy + ry * k), new(cx - rx, cy));
        CurveTo(new(cx - rx, cy - ry * k), new(cx - rx * k, cy - ry), new(cx, cy - ry));
        CurveTo(new(cx + rx * k, cy - ry), new(cx + rx, cy - ry * k), new(cx + rx, cy));
        return Close();
    }

    public void Append(PathData other)
    {
        Figures.AddRange(other.Figures);
        _current = null;
    }

    public PathData Transform(Func<PdfPoint, PdfPoint> map)
    {
        var result = new PathData();
        foreach (var f in Figures)
        {
            var nf = new PathFigure(map(f.Start)) { Closed = f.Closed };
            foreach (var s in f.Segments)
            {
                nf.Segments.Add(s.Kind == SegmentKind.Line
                    ? new PathSegment(SegmentKind.Line, default, default, map(s.End))
                    : new PathSegment(SegmentKind.Cubic, map(s.C1), map(s.C2), map(s.End)));
            }
            result.Figures.Add(nf);
        }
        return result;
    }

    /// <summary>Umgebendes Rechteck aus Stütz- und Kontrollpunkten (bei Kurven etwas größer als nötig, nie kleiner).</summary>
    public PdfRect Bounds() => PdfRect.Bounds(AllPoints());

    private IEnumerable<PdfPoint> AllPoints()
    {
        foreach (var f in Figures)
        {
            yield return f.Start;
            foreach (var s in f.Segments)
            {
                if (s.Kind == SegmentKind.Cubic)
                {
                    yield return s.C1;
                    yield return s.C2;
                }
                yield return s.End;
            }
        }
    }

    /// <summary>Wandelt jede Figur in einen Streckenzug um (Kurven in festen Schritten).</summary>
    public IEnumerable<(IReadOnlyList<PdfPoint> Points, bool Closed)> Flatten(int curveSteps = 12)
    {
        foreach (var f in Figures)
        {
            var points = new List<PdfPoint> { f.Start };
            var last = f.Start;
            foreach (var s in f.Segments)
            {
                if (s.Kind == SegmentKind.Line)
                {
                    points.Add(s.End);
                }
                else
                {
                    for (var i = 1; i <= curveSteps; i++)
                    {
                        var t = i / (double)curveSteps;
                        var u = 1 - t;
                        points.Add(last * (u * u * u) + s.C1 * (3 * u * u * t) + s.C2 * (3 * u * t * t) + s.End * (t * t * t));
                    }
                }
                last = s.End;
            }
            yield return (points, f.Closed);
        }
    }
}

/// <summary>Eine Zeichenebene einer Markierung: Pfad mit Linie und/oder Füllung.</summary>
/// <param name="IsText">Textumrisse; zählen nicht zum Rahmen der Anmerkung und werden auf das Textfeld beschnitten.</param>
/// <param name="EvenOdd">Füllregel Gerade-Ungerade, damit innere Figuren (Ausschnitte) ausgespart bleiben.</param>
public sealed record ShapeLayer(PathData Path, PdfColor? Stroke, double StrokeWidth, PdfColor? Fill, bool IsText = false, PdfRect? Clip = null, bool EvenOdd = false);

/// <summary>Fertige Zeichnung einer Markierung in Benutzerkoordinaten.</summary>
public sealed record MarkupShape(IReadOnlyList<ShapeLayer> Layers, double Opacity, ShapeImage? Image = null)
{
    /// <summary>Rahmen der Anmerkung (PDF-Schlüssel Rect): alle Nicht-Text-Ebenen plus halbe Linienbreite und 1 pt Rand.</summary>
    public PdfRect Bounds()
    {
        PdfRect? result = null;
        foreach (var layer in Layers.Where(l => !l.IsText && !l.Path.IsEmpty))
        {
            var b = layer.Path.Bounds().Inflate((layer.Stroke is null ? 0 : layer.StrokeWidth / 2) + 1);
            result = result is { } r ? r.Union(b) : b;
        }
        if (Image is { } image)
        {
            var b = image.Bounds.Inflate(1);
            result = result is { } r ? r.Union(b) : b;
        }
        return result ?? default;
    }
}

/// <summary>
/// Bild in einer Zeichnung. Die Matrix (A, B, C, D, E, F) bildet das Einheitsquadrat des Bildes (0 bis 1, y nach oben,
/// Bildoberkante bei y = 1) auf Benutzerkoordinaten ab, wie die Bildmatrix im PDF-Inhaltsstrom.
/// </summary>
public sealed record ShapeImage(MarkupImage Image, double A, double B, double C, double D, double E, double F)
{
    public PdfPoint Map(double x, double y) => new(A * x + C * y + E, B * x + D * y + F);

    public PdfRect Bounds => PdfRect.Bounds([Map(0, 0), Map(1, 0), Map(0, 1), Map(1, 1)]);
}

/// <summary>Setzt Text in Umrisse um. Die Oberfläche liefert eine Umsetzung mit echter Schrift.</summary>
public interface ITextOutliner
{
    /// <summary>
    /// Setzt <paramref name="text"/> mit Zeilenumbruch bei <paramref name="maxWidth"/> Punkt. Koordinaten der
    /// Umrisse: Ursprung oben links, x nach rechts, y nach unten, Einheit Punkt.
    /// </summary>
    TextOutline Layout(string text, double fontSize, double maxWidth);
}

public sealed record TextOutline(PathData Path, double Width, double Height);

/// <summary>
/// Einfacher Ersatz ohne Schrift: jedes Zeichen als schmaler Block. Nur für Tests und als Rückfall, wenn keine
/// Schrift verfügbar ist.
/// </summary>
public sealed class BlockTextOutliner : ITextOutliner
{
    public TextOutline Layout(string text, double fontSize, double maxWidth)
    {
        var path = new PathData();
        var advance = fontSize * 0.55;
        var lineHeight = fontSize * 1.25;
        var perLine = Math.Max(1, (int)(maxWidth / advance));
        var y = 0.0;
        var width = 0.0;
        foreach (var rawLine in text.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine;
            do
            {
                var part = line.Length > perLine ? line[..perLine] : line;
                line = line.Length > perLine ? line[perLine..] : "";
                for (var i = 0; i < part.Length; i++)
                {
                    if (!char.IsWhiteSpace(part[i]))
                    {
                        var x = i * advance;
                        path.Rectangle(new PdfRect(x + advance * 0.15, y + lineHeight * 0.2, x + advance * 0.85, y + lineHeight * 0.9));
                    }
                }
                width = Math.Max(width, part.Length * advance);
                y += lineHeight;
            }
            while (line.Length > 0);
        }
        return new TextOutline(path, width, y);
    }
}
