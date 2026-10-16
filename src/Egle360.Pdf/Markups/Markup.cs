namespace Egle360.Pdf.Markups;

/// <summary>Arten von Markierungen. Die Zuordnung zu PDF-Anmerkungstypen steht in <see cref="MarkupKinds"/>.</summary>
public enum MarkupKind
{
    Rectangle,
    Ellipse,
    Line,
    Arrow,
    Polyline,
    Polygon,
    Cloud,
    Freehand,
    TextBox,
    Note,
    Highlight,
}

/// <summary>Punkt im PDF-Benutzerkoordinatensystem (Punkt, y nach oben, ohne Seitendrehung).</summary>
public readonly record struct PdfPoint(double X, double Y)
{
    public static PdfPoint operator +(PdfPoint a, PdfPoint b) => new(a.X + b.X, a.Y + b.Y);

    public static PdfPoint operator -(PdfPoint a, PdfPoint b) => new(a.X - b.X, a.Y - b.Y);

    public static PdfPoint operator *(PdfPoint a, double f) => new(a.X * f, a.Y * f);

    public double Length => Math.Sqrt(X * X + Y * Y);

    public double DistanceTo(PdfPoint other) => (this - other).Length;
}

/// <summary>Achsparalleles Rechteck im PDF-Benutzerkoordinatensystem.</summary>
public readonly record struct PdfRect(double Left, double Bottom, double Right, double Top)
{
    public double Width => Right - Left;

    public double Height => Top - Bottom;

    public static PdfRect FromPoints(PdfPoint a, PdfPoint b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X), Math.Max(a.Y, b.Y));

    public static PdfRect Bounds(IEnumerable<PdfPoint> points)
    {
        double l = double.PositiveInfinity, b = double.PositiveInfinity, r = double.NegativeInfinity, t = double.NegativeInfinity;
        foreach (var p in points)
        {
            l = Math.Min(l, p.X);
            b = Math.Min(b, p.Y);
            r = Math.Max(r, p.X);
            t = Math.Max(t, p.Y);
        }
        return double.IsInfinity(l) ? default : new PdfRect(l, b, r, t);
    }

    public PdfRect Inflate(double d) => new(Left - d, Bottom - d, Right + d, Top + d);

    public PdfRect Union(PdfRect o) => new(Math.Min(Left, o.Left), Math.Min(Bottom, o.Bottom), Math.Max(Right, o.Right), Math.Max(Top, o.Top));

    public bool Contains(PdfPoint p) => p.X >= Left && p.X <= Right && p.Y >= Bottom && p.Y <= Top;

    public bool Intersects(PdfRect o) => Left <= o.Right && o.Left <= Right && Bottom <= o.Top && o.Bottom <= Top;
}

/// <summary>Darstellung einer Markierung.</summary>
/// <param name="Stroke">Linien- und Textfarbe.</param>
/// <param name="Fill">Füllfarbe; null für keine Füllung.</param>
/// <param name="Opacity">Deckkraft 0 bis 1 für die ganze Markierung.</param>
/// <param name="LineWidth">Linienbreite in Punkt.</param>
/// <param name="FontSize">Schriftgröße in Punkt (Textfeld).</param>
public sealed record MarkupStyle(PdfColor Stroke, PdfColor? Fill, double Opacity, double LineWidth, double FontSize = 12)
{
    public static MarkupStyle DefaultFor(MarkupKind kind) => kind switch
    {
        MarkupKind.Highlight => new MarkupStyle(new PdfColor(255, 220, 0), new PdfColor(255, 220, 0), 0.4, 0),
        MarkupKind.Note => new MarkupStyle(new PdfColor(40, 40, 40), new PdfColor(255, 214, 10), 1, 0.75),
        MarkupKind.TextBox => new MarkupStyle(new PdfColor(200, 30, 30), null, 1, 1, 12),
        _ => new MarkupStyle(new PdfColor(200, 30, 30), null, 1, 1.5),
    };
}

/// <summary>
/// Eine Markierung auf einer Seite. Unveränderlich; Änderungen erzeugen eine neue Instanz mit derselben
/// <see cref="Id"/>. Gespeichert wird sie als PDF-Anmerkung (siehe <see cref="MarkupKinds"/>).
/// </summary>
/// <param name="Id">Eindeutiger Name (PDF-Schlüssel NM).</param>
/// <param name="Paths">
/// Geometrie in Benutzerkoordinaten. Rechteck, Ellipse, Textfeld und Hervorhebung: zwei Eckpunkte. Linie und Pfeil:
/// Anfang und Ende. Polylinie, Polygon, Wolke: alle Eckpunkte. Freihand: ein Pfad je Strich. Notiz: linke obere Ecke.
/// </param>
/// <param name="Text">Inhalt (Textfeld, Notiz) oder Kommentar (alle anderen).</param>
/// <param name="TextRotation">Seitendrehung beim Anlegen (0, 90, 180, 270), damit Text auf dem Bildschirm aufrecht steht.</param>
public sealed record Markup(
    string Id,
    MarkupKind Kind,
    IReadOnlyList<IReadOnlyList<PdfPoint>> Paths,
    MarkupStyle Style,
    string Text,
    string Author,
    DateTimeOffset Created,
    DateTimeOffset Modified,
    int TextRotation = 0)
{
    public IReadOnlyList<PdfPoint> Points => Paths.Count > 0 ? Paths[0] : [];

    public static string NewId() => "e360-" + Guid.NewGuid().ToString("N");

    public static Markup Create(MarkupKind kind, IReadOnlyList<IReadOnlyList<PdfPoint>> paths, MarkupStyle style, string author, int textRotation = 0, string text = "")
    {
        var now = DateTimeOffset.Now;
        return new Markup(NewId(), kind, paths, style, text, author, now, now, textRotation);
    }

    /// <summary>Kopie mit neuer Geometrie und aktuellem Änderungsdatum.</summary>
    public Markup WithPaths(IReadOnlyList<IReadOnlyList<PdfPoint>> paths) => this with { Paths = paths, Modified = DateTimeOffset.Now };

    public Markup WithStyle(MarkupStyle style) => this with { Style = style, Modified = DateTimeOffset.Now };

    public Markup WithText(string text) => this with { Text = text, Modified = DateTimeOffset.Now };
}

/// <summary>Eigenschaften der Markierungsarten.</summary>
public static class MarkupKinds
{
    /// <summary>Deutsche Bezeichnung; wird auch als Betreff (Subj) in die PDF-Anmerkung geschrieben.</summary>
    public static string DisplayName(MarkupKind kind) => kind switch
    {
        MarkupKind.Rectangle => "Rechteck",
        MarkupKind.Ellipse => "Ellipse",
        MarkupKind.Line => "Linie",
        MarkupKind.Arrow => "Pfeil",
        MarkupKind.Polyline => "Polylinie",
        MarkupKind.Polygon => "Polygon",
        MarkupKind.Cloud => "Wolke",
        MarkupKind.Freehand => "Freihand",
        MarkupKind.TextBox => "Textfeld",
        MarkupKind.Note => "Notiz",
        MarkupKind.Highlight => "Hervorhebung",
        _ => kind.ToString(),
    };

    /// <summary>Arten, die durch Aufziehen eines Rechtecks entstehen.</summary>
    public static bool IsBox(MarkupKind kind) => kind is MarkupKind.Rectangle or MarkupKind.Ellipse or MarkupKind.TextBox or MarkupKind.Highlight;

    /// <summary>Arten mit beliebig vielen Eckpunkten, die per Klick gesetzt werden.</summary>
    public static bool IsMultiPoint(MarkupKind kind) => kind is MarkupKind.Polyline or MarkupKind.Polygon or MarkupKind.Cloud;

    public static bool IsClosed(MarkupKind kind) => kind is MarkupKind.Polygon or MarkupKind.Cloud;

    public static bool CanFill(MarkupKind kind) => kind is MarkupKind.Rectangle or MarkupKind.Ellipse or MarkupKind.Polygon or MarkupKind.Cloud or MarkupKind.TextBox;

    public static bool HasText(MarkupKind kind) => kind is MarkupKind.TextBox or MarkupKind.Note;

    /// <summary>Größe des Notizsymbols in Punkt.</summary>
    public const double NoteSize = 20;
}
