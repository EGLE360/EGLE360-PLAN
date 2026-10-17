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

/// <summary>Drehen von Markierungen um einen Mittelpunkt.</summary>
public static class MarkupRotation
{
    /// <summary>
    /// Arten, die sich um beliebige Winkel drehen lassen: alles, was aus Punkten besteht. Rechteckige Arten (Rechteck,
    /// Ellipse, Textfeld, Stempel, Sprechblase) bleiben achsenparallel und drehen nur in 90-Grad-Schritten.
    /// </summary>
    public static bool CanRotateFreely(MarkupKind kind) =>
        !MarkupKinds.IsBox(kind) && kind is not (MarkupKind.Callout or MarkupKind.Note);

    /// <summary>Text-Hervorhebungen folgen dem Text und werden nicht gedreht.</summary>
    public static bool CanRotate(MarkupKind kind) => !MarkupKinds.IsTextMarkup(kind);

    /// <summary>Mittelpunkt der Griffrahmen aller Markierungen.</summary>
    public static PdfPoint Center(IEnumerable<Markup> markups)
    {
        var r = markups.Select(MarkupGeometry.HandleRect).Aggregate((a, b) => a.Union(b));
        return new PdfPoint((r.Left + r.Right) / 2, (r.Bottom + r.Top) / 2);
    }

    /// <summary>
    /// Dreht um <paramref name="quarterTurns"/> Vierteldrehungen im Uhrzeigersinn (auf dem Bildschirm, unabhängig von
    /// der Seitendrehung). Texte von Textfeldern, Sprechblasen und Stempeln drehen mit.
    /// </summary>
    public static Markup RotateQuarter(Markup m, int quarterTurns, PdfPoint center)
    {
        var turns = ((quarterTurns % 4) + 4) % 4;
        if (turns == 0 || !CanRotate(m.Kind))
        {
            return m;
        }
        // Im Uhrzeigersinn heißt in PDF-Koordinaten (y nach oben) ein negativer Winkel.
        PdfPoint Turn(PdfPoint p)
        {
            var d = p - center;
            return turns switch
            {
                1 => new PdfPoint(center.X + d.Y, center.Y - d.X),
                2 => new PdfPoint(center.X - d.X, center.Y - d.Y),
                _ => new PdfPoint(center.X - d.Y, center.Y + d.X),
            };
        }
        IReadOnlyList<IReadOnlyList<PdfPoint>> paths = [.. m.Paths.Select(p => (IReadOnlyList<PdfPoint>)[.. p.Select(Turn)])];
        if ((MarkupKinds.IsBox(m.Kind) || m.Kind == MarkupKind.Callout) && paths[0].Count >= 2)
        {
            // Die gedrehten Ecken wieder als linke untere und rechte obere Ecke ablegen.
            var box = PdfRect.FromPoints(paths[0][0], paths[0][1]);
            paths = [[new PdfPoint(box.Left, box.Bottom), new PdfPoint(box.Right, box.Top)], .. paths.Skip(1)];
        }
        if (m.Kind == MarkupKind.Note && paths[0].Count >= 1)
        {
            // Die Notiz ist ein Symbol mit fester Größe; nur ihre Lage dreht sich um den Mittelpunkt des Symbols.
            var r = MarkupGeometry.HandleRect(m);
            var c = Turn(new PdfPoint((r.Left + r.Right) / 2, (r.Bottom + r.Top) / 2));
            paths = [[m.Points[0] + (c - new PdfPoint((r.Left + r.Right) / 2, (r.Bottom + r.Top) / 2))]];
            return m with { Paths = paths, Modified = DateTimeOffset.Now };
        }
        // Texte von Textfeldern und Stempeln drehen mit, ebenso Bilder; Beschriftungen von Messungen bleiben lesbar wie bisher.
        var textRotation = MarkupKinds.HasText(m.Kind) || m.Kind == MarkupKind.Image ? TextFrame.Normalize(m.TextRotation - 90 * turns) : m.TextRotation;
        return m with { Paths = paths, TextRotation = textRotation, Modified = DateTimeOffset.Now };
    }

    /// <summary>Dreht um einen beliebigen Winkel gegen den Uhrzeigersinn (in Grad, wie auf dem Bildschirm); nur für punktförmige Arten.</summary>
    public static Markup RotateFree(Markup m, double degreesCounterClockwise, PdfPoint center)
    {
        if (!CanRotateFreely(m.Kind) || degreesCounterClockwise % 360 == 0)
        {
            return m;
        }
        var a = degreesCounterClockwise * Math.PI / 180;
        var (sin, cos) = Math.SinCos(a);
        PdfPoint Turn(PdfPoint p)
        {
            var d = p - center;
            return new PdfPoint(center.X + d.X * cos - d.Y * sin, center.Y + d.X * sin + d.Y * cos);
        }
        return m with { Paths = [.. m.Paths.Select(p => (IReadOnlyList<PdfPoint>)[.. p.Select(Turn)])], Modified = DateTimeOffset.Now };
    }
}
