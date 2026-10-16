namespace Egle360.Pdf.Markups;

/// <summary>
/// Geometrie der Markierungen: Zeichnung, Trefferprüfung, Griffe und Verschieben. Reine Rechenlogik in
/// PDF-Benutzerkoordinaten.
/// </summary>
public static class MarkupGeometry
{
    /// <summary>Innenabstand des Textes im Textfeld in Punkt.</summary>
    public const double TextPadding = 4;

    /// <summary>Erzeugt die Zeichnung. Ohne <paramref name="outliner"/> fehlt bei Textfeldern nur der Text.</summary>
    public static MarkupShape BuildShape(Markup m, ITextOutliner? outliner)
    {
        var s = m.Style;
        var w = s.LineWidth;
        var layers = new List<ShapeLayer>();
        PdfColor? stroke = w > 0 ? s.Stroke : null;
        PdfColor? fill = MarkupKinds.CanFill(m.Kind) ? s.Fill : null;
        var pts = m.Points;

        switch (m.Kind)
        {
            case MarkupKind.Rectangle when pts.Count >= 2:
                layers.Add(new ShapeLayer(new PathData().Rectangle(PdfRect.FromPoints(pts[0], pts[1])), stroke, w, fill));
                break;
            case MarkupKind.Ellipse when pts.Count >= 2:
                layers.Add(new ShapeLayer(new PathData().Ellipse(PdfRect.FromPoints(pts[0], pts[1])), stroke, w, fill));
                break;
            case MarkupKind.Line when pts.Count >= 2:
                layers.Add(new ShapeLayer(new PathData().Polyline([pts[0], pts[1]], false), s.Stroke, w, null));
                break;
            case MarkupKind.Arrow when pts.Count >= 2:
                var arrow = new PathData().Polyline([pts[0], pts[1]], false);
                arrow.Append(ArrowHead(pts[0], pts[1], w));
                layers.Add(new ShapeLayer(arrow, s.Stroke, w, null));
                break;
            case MarkupKind.Polyline when pts.Count >= 2:
                layers.Add(new ShapeLayer(new PathData().Polyline(pts, false), s.Stroke, w, null));
                break;
            case MarkupKind.Polygon when pts.Count >= 2:
                layers.Add(new ShapeLayer(new PathData().Polyline(pts, true), stroke, w, fill));
                break;
            case MarkupKind.Cloud when pts.Count >= 2:
                layers.Add(new ShapeLayer(Cloud(pts, CloudArcLength(w)), stroke, w, fill));
                break;
            case MarkupKind.Freehand:
                var ink = new PathData();
                foreach (var path in m.Paths.Where(p => p.Count > 0))
                {
                    // Einzelne Punkte als winzige Strecke, damit sie sichtbar bleiben.
                    ink.Polyline(path.Count == 1 ? [path[0], path[0] + new PdfPoint(0.01, 0)] : path, false);
                }
                layers.Add(new ShapeLayer(ink, s.Stroke, w, null));
                break;
            case MarkupKind.TextBox when pts.Count >= 2:
                var box = PdfRect.FromPoints(pts[0], pts[1]);
                layers.Add(new ShapeLayer(new PathData().Rectangle(box), stroke, w, fill));
                if (outliner is not null && !string.IsNullOrEmpty(m.Text))
                {
                    var frame = TextFrame.ForBox(box, m.TextRotation);
                    var outline = outliner.Layout(m.Text, s.FontSize, Math.Max(1, frame.Width - 2 * TextPadding));
                    var text = outline.Path.Transform(p => frame.ToUser(new PdfPoint(p.X + TextPadding, p.Y + TextPadding)));
                    layers.Add(new ShapeLayer(text, null, 0, s.Stroke, IsText: true, Clip: box));
                }
                break;
            case MarkupKind.Note when pts.Count >= 1:
                layers.AddRange(NoteIcon(pts[0], m.TextRotation, s));
                break;
            case MarkupKind.Highlight when pts.Count >= 2:
                layers.Add(new ShapeLayer(new PathData().Rectangle(PdfRect.FromPoints(pts[0], pts[1])), null, 0, s.Fill ?? s.Stroke));
                break;
        }
        return new MarkupShape(layers, Math.Clamp(s.Opacity, 0, 1));
    }

    /// <summary>Rahmen der Anmerkung (PDF-Schlüssel Rect).</summary>
    public static PdfRect Bounds(Markup m) => BuildShape(m, null).Bounds();

    // ---------- Formen ----------

    /// <summary>Offene Pfeilspitze am Ende <paramref name="to"/>.</summary>
    public static PathData ArrowHead(PdfPoint from, PdfPoint to, double lineWidth)
    {
        var d = to - from;
        var len = d.Length;
        var path = new PathData();
        if (len < 1e-9)
        {
            return path;
        }
        var head = Math.Min(len, 8 + 2.5 * lineWidth);
        var dir = d * (1 / len);
        var normal = new PdfPoint(-dir.Y, dir.X);
        const double tan = 0.4663; // tan(25°)
        var back = to - dir * head;
        path.MoveTo(back + normal * (head * tan)).LineTo(to).LineTo(back - normal * (head * tan));
        return path;
    }

    /// <summary>Bogenlänge der Wolke in Punkt, abhängig von der Linienbreite.</summary>
    public static double CloudArcLength(double lineWidth) => Math.Max(12, 8 * lineWidth);

    /// <summary>
    /// Wolkenrand entlang eines geschlossenen Polygons: Jede Kante wird in gleich lange Abschnitte geteilt, über
    /// jeden Abschnitt spannt sich ein nach außen gewölbter Bogen.
    /// </summary>
    public static PathData Cloud(IReadOnlyList<PdfPoint> polygon, double arcLength)
    {
        var path = new PathData();
        var pts = RemoveDuplicates(polygon);
        if (pts.Count < 2)
        {
            return path;
        }
        // Außen liegt bei gegen den Uhrzeigersinn umlaufenden Polygonen rechts der Laufrichtung.
        var orientation = SignedArea(pts) >= 0 ? 1.0 : -1.0;
        path.MoveTo(pts[0]);
        for (var i = 0; i < pts.Count; i++)
        {
            var a = pts[i];
            var b = pts[(i + 1) % pts.Count];
            var edge = b - a;
            var len = edge.Length;
            if (len < 1e-9)
            {
                continue;
            }
            var n = Math.Max(1, (int)Math.Round(len / arcLength));
            var dir = edge * (1 / len);
            var outward = new PdfPoint(dir.Y, -dir.X) * orientation;
            var chord = len / n;
            var bulge = chord * 0.5 * 4 / 3; // Kontrollpunktabstand für einen annähernd halbkreisförmigen Bogen
            for (var k = 0; k < n; k++)
            {
                var p0 = a + dir * (chord * k);
                var p1 = a + dir * (chord * (k + 1));
                path.CurveTo(p0 + outward * bulge, p1 + outward * bulge, p1);
            }
        }
        return path.Close();
    }

    private static IEnumerable<ShapeLayer> NoteIcon(PdfPoint origin, int rotation, MarkupStyle style)
    {
        const double s = MarkupKinds.NoteSize;
        var frame = new TextFrame(origin, rotation, s, s);
        var bubble = new PathData()
            .MoveTo(new(1, 1)).LineTo(new(s - 1, 1)).LineTo(new(s - 1, s * 0.72)).LineTo(new(s * 0.42, s * 0.72))
            .LineTo(new(s * 0.22, s - 1)).LineTo(new(s * 0.26, s * 0.72)).LineTo(new(1, s * 0.72)).Close();
        var lines = new PathData();
        foreach (var y in new[] { 0.22, 0.38, 0.54 })
        {
            lines.MoveTo(new(s * 0.2, s * y)).LineTo(new(s * 0.8, s * y));
        }
        yield return new ShapeLayer(bubble.Transform(frame.ToUser), style.Stroke, Math.Max(0.5, style.LineWidth), style.Fill ?? new PdfColor(255, 214, 10));
        yield return new ShapeLayer(lines.Transform(frame.ToUser), style.Stroke, Math.Max(0.5, style.LineWidth), null);
    }

    // ---------- Trefferprüfung ----------

    /// <summary>
    /// Prüft, ob <paramref name="p"/> die Markierung trifft: auf der Linie (mit Toleranz) oder innerhalb gefüllter
    /// Flächen, Textfelder, Notizen und Hervorhebungen.
    /// </summary>
    public static bool HitTest(Markup m, PdfPoint p, double tolerance)
    {
        var shape = BuildShape(m, null);
        if (!shape.Bounds().Inflate(tolerance).Contains(p))
        {
            return false;
        }
        var areaCounts = m.Kind is MarkupKind.TextBox or MarkupKind.Note or MarkupKind.Highlight;
        foreach (var layer in shape.Layers)
        {
            foreach (var (points, closed) in layer.Path.Flatten())
            {
                if ((areaCounts || layer.Fill is not null) && closed && PointInPolygon(points, p))
                {
                    return true;
                }
                var reach = tolerance + (layer.Stroke is null ? 0 : layer.StrokeWidth / 2);
                if (DistanceToPolyline(points, closed, p) <= reach)
                {
                    return true;
                }
            }
        }
        return false;
    }

    /// <summary>Oberste getroffene Markierung (die zuletzt gezeichnete liegt oben).</summary>
    public static Markup? HitTestTopmost(IReadOnlyList<Markup> markups, PdfPoint p, double tolerance)
    {
        for (var i = markups.Count - 1; i >= 0; i--)
        {
            if (HitTest(markups[i], p, tolerance))
            {
                return markups[i];
            }
        }
        return null;
    }

    public static double DistanceToSegment(PdfPoint p, PdfPoint a, PdfPoint b)
    {
        var ab = b - a;
        var len2 = ab.X * ab.X + ab.Y * ab.Y;
        if (len2 < 1e-18)
        {
            return p.DistanceTo(a);
        }
        var t = Math.Clamp(((p.X - a.X) * ab.X + (p.Y - a.Y) * ab.Y) / len2, 0, 1);
        return p.DistanceTo(a + ab * t);
    }

    public static double DistanceToPolyline(IReadOnlyList<PdfPoint> points, bool closed, PdfPoint p)
    {
        if (points.Count == 1)
        {
            return p.DistanceTo(points[0]);
        }
        var best = double.PositiveInfinity;
        var count = closed ? points.Count : points.Count - 1;
        for (var i = 0; i < count; i++)
        {
            best = Math.Min(best, DistanceToSegment(p, points[i], points[(i + 1) % points.Count]));
        }
        return best;
    }

    /// <summary>Gerade-Ungerade-Regel.</summary>
    public static bool PointInPolygon(IReadOnlyList<PdfPoint> poly, PdfPoint p)
    {
        var inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            var a = poly[i];
            var b = poly[j];
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X)
            {
                inside = !inside;
            }
        }
        return inside;
    }

    public static double SignedArea(IReadOnlyList<PdfPoint> poly)
    {
        var sum = 0.0;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            sum += (poly[j].X * poly[i].Y) - (poly[i].X * poly[j].Y);
        }
        return sum / 2;
    }

    // ---------- Griffe und Verschieben ----------

    /// <summary>Lage der acht Rahmengriffe als Anteil des Rechtecks (links unten beginnend, gegen den Uhrzeigersinn).</summary>
    private static readonly (double X, double Y)[] BoxHandles =
        [(0, 0), (0.5, 0), (1, 0), (1, 0.5), (1, 1), (0.5, 1), (0, 1), (0, 0.5)];

    /// <summary>Griffpunkte zum Bearbeiten. Rahmenarten und Freihand: acht Rahmengriffe; Linienarten: Eckpunkte; Notiz: keine.</summary>
    public static IReadOnlyList<PdfPoint> Handles(Markup m)
    {
        if (m.Kind == MarkupKind.Note)
        {
            return [];
        }
        if (UsesVertexHandles(m.Kind))
        {
            return m.Points;
        }
        var r = HandleRect(m);
        return BoxHandles.Select(h => new PdfPoint(r.Left + h.X * r.Width, r.Bottom + h.Y * r.Height)).ToArray();
    }

    public static bool UsesVertexHandles(MarkupKind kind) =>
        kind is MarkupKind.Line or MarkupKind.Arrow or MarkupKind.Polyline or MarkupKind.Polygon or MarkupKind.Cloud;

    /// <summary>Zieht einen Griff an die Position <paramref name="to"/>.</summary>
    public static Markup DragHandle(Markup m, int handle, PdfPoint to)
    {
        if (UsesVertexHandles(m.Kind))
        {
            if (handle < 0 || handle >= m.Points.Count)
            {
                return m;
            }
            var pts = m.Points.ToArray();
            pts[handle] = to;
            return m.WithPaths([pts]);
        }
        if (m.Kind == MarkupKind.Note || handle < 0 || handle >= BoxHandles.Length)
        {
            return m;
        }
        var r = HandleRect(m);
        var (hx, hy) = BoxHandles[handle];
        double l = r.Left, b = r.Bottom, rr = r.Right, t = r.Top;
        if (hx == 0) l = to.X;
        if (hx == 1) rr = to.X;
        if (hy == 0) b = to.Y;
        if (hy == 1) t = to.Y;
        var target = new PdfRect(Math.Min(l, rr), Math.Min(b, t), Math.Max(l, rr), Math.Max(b, t));
        return ResizeTo(m, target);
    }

    /// <summary>Setzt den Rahmen einer Rahmenart oder skaliert eine Freihandzeichnung auf das Zielrechteck.</summary>
    public static Markup ResizeTo(Markup m, PdfRect target)
    {
        if (m.Kind == MarkupKind.Freehand)
        {
            var r = HandleRect(m);
            var sx = r.Width > 1e-9 ? target.Width / r.Width : 1;
            var sy = r.Height > 1e-9 ? target.Height / r.Height : 1;
            return m.WithPaths(m.Paths.Select(path => (IReadOnlyList<PdfPoint>)path
                .Select(p => new PdfPoint(target.Left + (p.X - r.Left) * sx, target.Bottom + (p.Y - r.Bottom) * sy)).ToArray()).ToArray());
        }
        return m.WithPaths([new[] { new PdfPoint(target.Left, target.Bottom), new PdfPoint(target.Right, target.Top) }]);
    }

    public static Markup Translate(Markup m, double dx, double dy)
    {
        var d = new PdfPoint(dx, dy);
        return m.WithPaths(m.Paths.Select(path => (IReadOnlyList<PdfPoint>)path.Select(p => p + d).ToArray()).ToArray());
    }

    /// <summary>Rechteck, an dem die Rahmengriffe sitzen (Geometrie ohne Linienbreite).</summary>
    public static PdfRect HandleRect(Markup m) => PdfRect.Bounds(m.Paths.SelectMany(p => p));

    // ---------- Hilfen ----------

    /// <summary>Vereinfacht einen Streckenzug (Douglas-Peucker), damit Freihandstriche nicht unnötig groß werden.</summary>
    public static IReadOnlyList<PdfPoint> Simplify(IReadOnlyList<PdfPoint> points, double tolerance)
    {
        if (points.Count < 3)
        {
            return points.ToArray();
        }
        var keep = new bool[points.Count];
        keep[0] = keep[^1] = true;
        var stack = new Stack<(int, int)>();
        stack.Push((0, points.Count - 1));
        while (stack.Count > 0)
        {
            var (first, last) = stack.Pop();
            var maxDist = 0.0;
            var index = -1;
            for (var i = first + 1; i < last; i++)
            {
                var d = DistanceToSegment(points[i], points[first], points[last]);
                if (d > maxDist)
                {
                    maxDist = d;
                    index = i;
                }
            }
            if (index >= 0 && maxDist > tolerance)
            {
                keep[index] = true;
                stack.Push((first, index));
                stack.Push((index, last));
            }
        }
        return points.Where((_, i) => keep[i]).ToArray();
    }

    private static List<PdfPoint> RemoveDuplicates(IReadOnlyList<PdfPoint> pts)
    {
        var result = new List<PdfPoint>();
        foreach (var p in pts)
        {
            if (result.Count == 0 || result[^1].DistanceTo(p) > 1e-6)
            {
                result.Add(p);
            }
        }
        if (result.Count > 1 && result[0].DistanceTo(result[^1]) < 1e-6)
        {
            result.RemoveAt(result.Count - 1);
        }
        return result;
    }
}

/// <summary>
/// Lokales Koordinatensystem für aufrecht stehenden Text und Symbole: Ursprung oben links, x nach rechts, y nach
/// unten, so wie die Seite auf dem Bildschirm erscheint. Berücksichtigt die Seitendrehung.
/// </summary>
public readonly record struct TextFrame(PdfPoint Origin, int Rotation, double Width, double Height)
{
    /// <summary>Rahmen für ein Rechteck in Benutzerkoordinaten auf einer um <paramref name="rotation"/> Grad gedrehten Seite.</summary>
    public static TextFrame ForBox(PdfRect box, int rotation) => Normalize(rotation) switch
    {
        90 => new TextFrame(new PdfPoint(box.Left, box.Bottom), 90, box.Height, box.Width),
        180 => new TextFrame(new PdfPoint(box.Right, box.Bottom), 180, box.Width, box.Height),
        270 => new TextFrame(new PdfPoint(box.Right, box.Top), 270, box.Height, box.Width),
        _ => new TextFrame(new PdfPoint(box.Left, box.Top), 0, box.Width, box.Height),
    };

    public PdfPoint ToUser(PdfPoint local) => Normalize(Rotation) switch
    {
        90 => new PdfPoint(Origin.X + local.Y, Origin.Y + local.X),
        180 => new PdfPoint(Origin.X - local.X, Origin.Y + local.Y),
        270 => new PdfPoint(Origin.X - local.Y, Origin.Y - local.X),
        _ => new PdfPoint(Origin.X + local.X, Origin.Y - local.Y),
    };

    public static int Normalize(int rotation) => ((rotation % 360) + 360) % 360;
}
