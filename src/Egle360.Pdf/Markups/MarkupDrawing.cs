namespace Egle360.Pdf.Markups;

/// <summary>
/// Ablauf beim Zeichnen einer neuen Markierung, unabhängig von der Oberfläche. Rahmenarten, Linie und Pfeil
/// entstehen durch Ziehen, Freihand durch Zeichnen bei gedrückter Taste, Polylinie, Polygon und Wolke durch einzelne
/// Klicks (Abschluss mit Doppelklick oder Eingabetaste), die Notiz durch einen Klick. Der Stempel entsteht durch
/// Aufziehen oder per Klick in Standardgröße; <paramref name="text"/> ist seine Aufschrift.
/// </summary>
public sealed class MarkupDrawing(MarkupKind kind, MarkupStyle style, string author, int pageRotation, double minSize, MeasureScale? scale = null, string text = "")
{
    private readonly List<PdfPoint> _points = [];
    private bool _pressed;

    public MarkupKind Kind { get; } = kind;

    /// <summary>True, sobald mindestens ein Punkt gesetzt ist.</summary>
    public bool IsActive => _points.Count > 0;

    /// <summary>Standardgröße eines per Klick gesetzten Textfelds in Punkt (Breite, Höhe auf dem Bildschirm).</summary>
    public static (double Width, double Height) DefaultTextBox => (160, 40);

    /// <summary>
    /// Bezugspunkt für das Einrasten mit gedrückter Umschalttaste: der zuletzt gesetzte Punkt bei Klickarten, sonst der
    /// Anfangspunkt. Null, solange nichts gesetzt ist oder die Art nicht einrastet (Freihand, Notiz, Anzahl).
    /// </summary>
    public PdfPoint? Anchor => Kind is MarkupKind.Freehand or MarkupKind.Note or MarkupKind.Count || _points.Count == 0
        ? null
        : MarkupKinds.IsMultiPoint(Kind) ? (_points.Count >= 2 ? _points[^2] : null) : _points[0];

    /// <summary>
    /// Rastet <paramref name="p"/> relativ zu <paramref name="anchor"/> ein: Rahmenarten werden quadratisch, Linien
    /// waagrecht, senkrecht oder unter 45 Grad.
    /// </summary>
    public static PdfPoint Constrain(MarkupKind kind, PdfPoint anchor, PdfPoint p)
    {
        var d = p - anchor;
        if (MarkupKinds.IsBox(kind))
        {
            var size = Math.Max(Math.Abs(d.X), Math.Abs(d.Y));
            return anchor + new PdfPoint(Math.CopySign(size, d.X), Math.CopySign(size, d.Y));
        }
        var length = d.Length;
        if (length < 1e-9)
        {
            return p;
        }
        var angle = Math.Round(Math.Atan2(d.Y, d.X) / (Math.PI / 4)) * (Math.PI / 4);
        // Auf die Achse projizieren, damit die Länge der gewünschten Richtung entspricht.
        var dir = new PdfPoint(Math.Cos(angle), Math.Sin(angle));
        var along = d.X * dir.X + d.Y * dir.Y;
        return anchor + dir * along;
    }

    public void Press(PdfPoint p)
    {
        _pressed = true;
        if (MarkupKinds.IsMultiPoint(Kind))
        {
            if (_points.Count == 0)
            {
                _points.Add(p);
            }
            else
            {
                _points[^1] = p;
            }
            _points.Add(p); // beweglicher Punkt bis zum nächsten Klick
            return;
        }
        _points.Clear();
        _points.Add(p);
        if (Kind != MarkupKind.Freehand)
        {
            _points.Add(p);
        }
    }

    public void Move(PdfPoint p)
    {
        if (_points.Count == 0)
        {
            return;
        }
        if (Kind == MarkupKind.Freehand)
        {
            if (_pressed && p.DistanceTo(_points[^1]) >= minSize / 4)
            {
                _points.Add(p);
            }
            return;
        }
        if (_pressed || MarkupKinds.IsMultiPoint(Kind))
        {
            _points[^1] = p;
        }
    }

    /// <summary>Loslassen der Maustaste. Liefert die fertige Markierung, wenn die Art damit abgeschlossen ist.</summary>
    public Markup? Release(PdfPoint p)
    {
        if (!_pressed)
        {
            return null;
        }
        _pressed = false;
        if (MarkupKinds.IsMultiPoint(Kind))
        {
            _points[^1] = p;
            // Der Winkel ist mit dem dritten Punkt fertig (zwei Schenkel, Scheitel in der Mitte).
            return Kind == MarkupKind.Angle && _points.Count >= 4 ? Finish() : null;
        }
        if (Kind == MarkupKind.Freehand)
        {
            if (p.DistanceTo(_points[^1]) > 1e-9)
            {
                _points.Add(p);
            }
            var stroke = MarkupGeometry.Simplify(_points, minSize / 8);
            var length = stroke.Zip(stroke.Skip(1), (a, b) => a.DistanceTo(b)).Sum();
            return Complete(length >= minSize ? [stroke] : null);
        }

        _points[^1] = p;
        var start = _points[0];
        var dragged = start.DistanceTo(p) >= minSize;
        return Kind switch
        {
            MarkupKind.Note => Complete([new[] { start }]),
            MarkupKind.Stamp when !dragged => Complete([StampAt(start)]),
            MarkupKind.Callout => Complete(CalloutPaths(start, p, dragged)),
            MarkupKind.TextBox or MarkupKind.Typewriter when !dragged => Complete([new[] { start, new TextFrame(start, pageRotation, 0, 0).ToUser(new PdfPoint(DefaultTextBox.Width, DefaultTextBox.Height)) }]),
            _ when !dragged => Complete(null),
            _ => Complete([new[] { start, p }]),
        };
    }

    /// <summary>Schließt eine Polylinie, ein Polygon oder eine Wolke ab. Liefert null bei zu wenigen Punkten.</summary>
    public Markup? Finish()
    {
        if (!MarkupKinds.IsMultiPoint(Kind) || _points.Count == 0)
        {
            return null;
        }
        // Der bewegliche Punkt entfällt; doppelte Punkte (z. B. vom Doppelklick) werden zusammengefasst.
        var vertices = new List<PdfPoint>();
        foreach (var p in _points.Take(_points.Count - 1))
        {
            if (vertices.Count == 0 || vertices[^1].DistanceTo(p) >= minSize / 4 || (Kind == MarkupKind.Count && !vertices.Contains(p)))
            {
                vertices.Add(p);
            }
        }
        return Complete(vertices.Count >= MarkupKinds.MinimumPoints(Kind) ? [vertices.ToArray()] : null);
    }

    public void Cancel()
    {
        _points.Clear();
        _pressed = false;
    }

    /// <summary>Vorschau des aktuellen Stands für die Anzeige; null, solange nichts zu sehen ist.</summary>
    public Markup? Preview
    {
        get
        {
            if (_points.Count == 0)
            {
                return null;
            }
            IReadOnlyList<PdfPoint> pts = Kind switch
            {
                MarkupKind.Note => [_points[0]],
                // Beim Zählen folgt kein Punkt dem Mauszeiger, sonst stimmt die angezeigte Anzahl nicht.
                MarkupKind.Count => _points.Take(Math.Max(1, _points.Count - 1)).ToArray(),
                _ => _points.ToArray(),
            };
            var previewKind = Kind switch
            {
                MarkupKind.Count => Kind,
                MarkupKind.Angle when pts.Count < 3 => MarkupKind.Polyline,
                MarkupKind.Perimeter or MarkupKind.Area when pts.Count < 3 => MarkupKind.Polylength,
                _ when MarkupKinds.IsMultiPoint(Kind) && pts.Count < 3 => MarkupKinds.IsMeasurement(Kind) ? MarkupKind.Polylength : MarkupKind.Polyline,
                _ => Kind,
            };
            IReadOnlyList<IReadOnlyList<PdfPoint>> paths = Kind == MarkupKind.Callout && pts.Count >= 2 ? CalloutPaths(pts[0], pts[^1], true) : [pts];
            return new Markup("preview", previewKind, paths, style, text, author, DateTimeOffset.Now, DateTimeOffset.Now, pageRotation, scale);
        }
    }

    /// <summary>
    /// Sprechblase von der Pfeilspitze <paramref name="tip"/> zum Textrahmen bei <paramref name="at"/>. Der Rahmen
    /// in Standardgröße liegt auf dem Bildschirm vom Spitzenpunkt weg; ohne Ziehen schräg rechts darüber.
    /// </summary>
    private IReadOnlyList<IReadOnlyList<PdfPoint>> CalloutPaths(PdfPoint tip, PdfPoint at, bool dragged)
    {
        var frame = new TextFrame(tip, pageRotation, 0, 0);
        var local = dragged ? frame.ToLocal(at) : new PdfPoint(40, -60);
        var (w, h) = DefaultTextBox;
        var left = local.X >= 0 ? local.X : local.X - w;
        var top = local.Y - h / 2;
        return [new[] { frame.ToUser(new PdfPoint(left, top)), frame.ToUser(new PdfPoint(left + w, top + h)) }, new[] { tip }];
    }

    /// <summary>Stempel in Standardgröße, mittig auf dem Klickpunkt.</summary>
    private PdfPoint[] StampAt(PdfPoint center)
    {
        var (w, h) = StampPreset.DefaultSize;
        var frame = new TextFrame(center, pageRotation, 0, 0);
        return [frame.ToUser(new PdfPoint(-w / 2, -h / 2)), frame.ToUser(new PdfPoint(w / 2, h / 2))];
    }

    private Markup? Complete(IReadOnlyList<IReadOnlyList<PdfPoint>>? paths)
    {
        Cancel();
        return paths is null ? null : Markup.Create(Kind, paths, style, author, pageRotation, text) with { Scale = MarkupKinds.NeedsScale(Kind) ? scale : null };
    }
}
