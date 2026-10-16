namespace Egle360.Pdf.Markups;

/// <summary>Ausrichtung mehrerer Markierungen, bezogen auf die Lage auf dem Bildschirm.</summary>
public enum MarkupAlignment
{
    Left,
    Center,
    Right,
    Top,
    Middle,
    Bottom,
    DistributeHorizontally,
    DistributeVertically,
}

/// <summary>Ausrichten und Verteilen. Links, oben usw. gelten so, wie die Seite mit ihrer Drehung angezeigt wird.</summary>
public static class MarkupArrange
{
    public static string DisplayName(MarkupAlignment a) => a switch
    {
        MarkupAlignment.Left => "Links ausrichten",
        MarkupAlignment.Center => "Horizontal zentrieren",
        MarkupAlignment.Right => "Rechts ausrichten",
        MarkupAlignment.Top => "Oben ausrichten",
        MarkupAlignment.Middle => "Vertikal zentrieren",
        MarkupAlignment.Bottom => "Unten ausrichten",
        MarkupAlignment.DistributeHorizontally => "Horizontal verteilen",
        MarkupAlignment.DistributeVertically => "Vertikal verteilen",
        _ => a.ToString(),
    };

    /// <summary>Verschobene Markierungen; unveränderte fehlen im Ergebnis. Verteilen braucht mindestens drei.</summary>
    public static IReadOnlyList<Markup> Align(IReadOnlyList<Markup> markups, MarkupAlignment alignment, int pageRotation)
    {
        if (markups.Count < 2 || (alignment is MarkupAlignment.DistributeHorizontally or MarkupAlignment.DistributeVertically && markups.Count < 3))
        {
            return [];
        }
        var frame = new TextFrame(default, pageRotation, 0, 0);
        // Rahmen auf dem Bildschirm: x nach rechts, y nach unten.
        var boxes = markups.Select(m =>
        {
            var r = MarkupGeometry.HandleRect(m);
            PdfPoint[] corners = [new(r.Left, r.Bottom), new(r.Right, r.Top), new(r.Left, r.Top), new(r.Right, r.Bottom)];
            return PdfRect.Bounds(corners.Select(frame.ToLocal));
        }).ToList();
        var left = boxes.Min(b => b.Left);
        var right = boxes.Max(b => b.Right);
        var top = boxes.Min(b => b.Bottom); // kleinstes y = oben
        var bottom = boxes.Max(b => b.Top);

        var shifts = new PdfPoint[markups.Count];
        switch (alignment)
        {
            case MarkupAlignment.Left:
                for (var i = 0; i < boxes.Count; i++) shifts[i] = new(left - boxes[i].Left, 0);
                break;
            case MarkupAlignment.Right:
                for (var i = 0; i < boxes.Count; i++) shifts[i] = new(right - boxes[i].Right, 0);
                break;
            case MarkupAlignment.Center:
                for (var i = 0; i < boxes.Count; i++) shifts[i] = new((left + right) / 2 - (boxes[i].Left + boxes[i].Right) / 2, 0);
                break;
            case MarkupAlignment.Top:
                for (var i = 0; i < boxes.Count; i++) shifts[i] = new(0, top - boxes[i].Bottom);
                break;
            case MarkupAlignment.Bottom:
                for (var i = 0; i < boxes.Count; i++) shifts[i] = new(0, bottom - boxes[i].Top);
                break;
            case MarkupAlignment.Middle:
                for (var i = 0; i < boxes.Count; i++) shifts[i] = new(0, (top + bottom) / 2 - (boxes[i].Bottom + boxes[i].Top) / 2);
                break;
            case MarkupAlignment.DistributeHorizontally:
                Distribute(boxes, shifts, b => (b.Left + b.Right) / 2, d => new PdfPoint(d, 0));
                break;
            case MarkupAlignment.DistributeVertically:
                Distribute(boxes, shifts, b => (b.Bottom + b.Top) / 2, d => new PdfPoint(0, d));
                break;
        }

        var origin = frame.ToUser(default);
        var result = new List<Markup>();
        for (var i = 0; i < markups.Count; i++)
        {
            if (shifts[i].Length < 1e-6)
            {
                continue;
            }
            var d = frame.ToUser(shifts[i]) - origin;
            result.Add(MarkupGeometry.Translate(markups[i], d.X, d.Y));
        }
        return result;
    }

    /// <summary>Gleiche Abstände der Mitten zwischen der ersten und der letzten Markierung.</summary>
    private static void Distribute(List<PdfRect> boxes, PdfPoint[] shifts, Func<PdfRect, double> center, Func<double, PdfPoint> along)
    {
        var order = Enumerable.Range(0, boxes.Count).OrderBy(i => center(boxes[i])).ToList();
        var first = center(boxes[order[0]]);
        var step = (center(boxes[order[^1]]) - first) / (order.Count - 1);
        for (var k = 1; k < order.Count - 1; k++)
        {
            var i = order[k];
            shifts[i] = along(first + k * step - center(boxes[i]));
        }
    }
}
