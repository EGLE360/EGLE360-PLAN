namespace Egle360.Pdf.Markups;

/// <summary>
/// Ablauf beim Zeichnen einer neuen Markierung, unabhängig von der Oberfläche. Rahmenarten, Linie und Pfeil
/// entstehen durch Ziehen, Freihand durch Zeichnen bei gedrückter Taste, Polylinie, Polygon und Wolke durch einzelne
/// Klicks (Abschluss mit Doppelklick oder Eingabetaste), die Notiz durch einen Klick.
/// </summary>
public sealed class MarkupDrawing(MarkupKind kind, MarkupStyle style, string author, int pageRotation, double minSize, MeasureScale? scale = null)
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
            return null;
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
            MarkupKind.TextBox when !dragged => Complete([new[] { start, new TextFrame(start, pageRotation, 0, 0).ToUser(new PdfPoint(DefaultTextBox.Width, DefaultTextBox.Height)) }]),
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
                MarkupKind.Perimeter or MarkupKind.Area when pts.Count < 3 => MarkupKind.Polylength,
                _ when MarkupKinds.IsMultiPoint(Kind) && pts.Count < 3 => MarkupKinds.IsMeasurement(Kind) ? MarkupKind.Polylength : MarkupKind.Polyline,
                _ => Kind,
            };
            return new Markup("preview", previewKind, [pts], style, "", author, DateTimeOffset.Now, DateTimeOffset.Now, pageRotation, scale);
        }
    }

    private Markup? Complete(IReadOnlyList<IReadOnlyList<PdfPoint>>? paths)
    {
        Cancel();
        return paths is null ? null : Markup.Create(Kind, paths, style, author, pageRotation) with { Scale = MarkupKinds.IsMeasurement(Kind) ? scale : null };
    }
}
