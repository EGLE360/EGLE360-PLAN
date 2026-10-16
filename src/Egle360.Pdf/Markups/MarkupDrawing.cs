namespace Egle360.Pdf.Markups;

/// <summary>
/// Ablauf beim Zeichnen einer neuen Markierung, unabhängig von der Oberfläche. Rahmenarten, Linie und Pfeil
/// entstehen durch Ziehen, Freihand durch Zeichnen bei gedrückter Taste, Polylinie, Polygon und Wolke durch einzelne
/// Klicks (Abschluss mit Doppelklick oder Eingabetaste), die Notiz durch einen Klick.
/// </summary>
public sealed class MarkupDrawing(MarkupKind kind, MarkupStyle style, string author, int pageRotation, double minSize)
{
    private readonly List<PdfPoint> _points = [];
    private bool _pressed;

    public MarkupKind Kind { get; } = kind;

    /// <summary>True, sobald mindestens ein Punkt gesetzt ist.</summary>
    public bool IsActive => _points.Count > 0;

    /// <summary>Standardgröße eines per Klick gesetzten Textfelds in Punkt (Breite, Höhe auf dem Bildschirm).</summary>
    public static (double Width, double Height) DefaultTextBox => (160, 40);

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
            if (vertices.Count == 0 || vertices[^1].DistanceTo(p) >= minSize / 4)
            {
                vertices.Add(p);
            }
        }
        var needed = MarkupKinds.IsClosed(Kind) ? 3 : 2;
        return Complete(vertices.Count >= needed ? [vertices.ToArray()] : null);
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
                _ when MarkupKinds.IsMultiPoint(Kind) && _points.Count == 2 => _points.ToArray(),
                _ => _points.ToArray(),
            };
            var previewKind = MarkupKinds.IsMultiPoint(Kind) && pts.Count < 3 ? MarkupKind.Polyline : Kind;
            return new Markup("preview", previewKind, [pts], style, "", author, DateTimeOffset.Now, DateTimeOffset.Now, pageRotation);
        }
    }

    private Markup? Complete(IReadOnlyList<IReadOnlyList<PdfPoint>>? paths)
    {
        Cancel();
        return paths is null ? null : Markup.Create(Kind, paths, style, author, pageRotation);
    }
}
