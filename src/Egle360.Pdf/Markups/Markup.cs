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

    // Messungen (Mengenermittlung). Werte ergeben sich aus der Geometrie und dem Maßstab der Messung.
    Length,
    Polylength,
    Perimeter,
    Area,
    Count,
    Angle,

    // Später ergänzt; neue Arten immer am Ende anfügen, die Zahlenwerte stehen in gespeicherten PDFs.
    Dimension,
    Stamp,
    Radius,
    Diameter,
    Callout,
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
        MarkupKind.Callout => new MarkupStyle(new PdfColor(200, 30, 30), PdfColor.White, 1, 1, 12),
        MarkupKind.Stamp => new MarkupStyle(StampPreset.Defaults[0].Color, null, 0.9, 2, 14),
        _ when MarkupKinds.IsMeasurement(kind) => new MarkupStyle(new PdfColor(22, 119, 210), null, 1, 1.5, 10),
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
/// Sprechblase: erster Pfad die beiden Ecken des Textrahmens, zweiter Pfad die Pfeilspitze.
/// </param>
/// <param name="Text">Inhalt (Textfeld, Notiz) oder Kommentar (alle anderen).</param>
/// <param name="TextRotation">Seitendrehung beim Anlegen (0, 90, 180, 270), damit Text auf dem Bildschirm aufrecht steht.</param>
/// <param name="Scale">Maßstab einer Messung; bei anderen Markierungen null.</param>
/// <param name="Depth">Höhe oder Tiefe in Metern: Flächen ergeben damit ein Volumen, Längen eine Wandfläche.</param>
/// <param name="Status">Prüf- oder Bearbeitungsstatus mit Person und Zeitpunkt.</param>
/// <param name="Locked">Gesperrt: nicht verschiebbar, nicht änderbar, nicht löschbar, bis die Sperre aufgehoben wird.</param>
/// <param name="UnitPrice">Einheitspreis in Euro je Mengeneinheit einer Messung.</param>
public sealed record Markup(
    string Id,
    MarkupKind Kind,
    IReadOnlyList<IReadOnlyList<PdfPoint>> Paths,
    MarkupStyle Style,
    string Text,
    string Author,
    DateTimeOffset Created,
    DateTimeOffset Modified,
    int TextRotation = 0,
    MeasureScale? Scale = null,
    double? Depth = null,
    MarkupStatus? Status = null,
    bool Locked = false,
    double? UnitPrice = null)
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

    public Markup WithScale(MeasureScale? scale) => this with { Scale = scale, Modified = DateTimeOffset.Now };

    public Markup WithStatus(MarkupStatus? status) => this with { Status = status, Modified = DateTimeOffset.Now };
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
        MarkupKind.Length => "Länge",
        MarkupKind.Polylength => "Polylänge",
        MarkupKind.Perimeter => "Umfang",
        MarkupKind.Area => "Fläche",
        MarkupKind.Count => "Anzahl",
        MarkupKind.Angle => "Winkel",
        MarkupKind.Dimension => "Bemaßung",
        MarkupKind.Stamp => "Stempel",
        MarkupKind.Radius => "Radius",
        MarkupKind.Diameter => "Durchmesser",
        MarkupKind.Callout => "Sprechblase",
        _ => kind.ToString(),
    };

    /// <summary>Arten, die durch Aufziehen eines Rechtecks entstehen.</summary>
    public static bool IsBox(MarkupKind kind) => kind is MarkupKind.Rectangle or MarkupKind.Ellipse or MarkupKind.TextBox or MarkupKind.Highlight or MarkupKind.Stamp;

    /// <summary>Arten mit beliebig vielen Eckpunkten, die per Klick gesetzt werden.</summary>
    public static bool IsMultiPoint(MarkupKind kind) =>
        kind is MarkupKind.Polyline or MarkupKind.Polygon or MarkupKind.Cloud
            or MarkupKind.Polylength or MarkupKind.Perimeter or MarkupKind.Area or MarkupKind.Count or MarkupKind.Angle;

    public static bool IsClosed(MarkupKind kind) => kind is MarkupKind.Polygon or MarkupKind.Cloud or MarkupKind.Perimeter or MarkupKind.Area;

    public static bool CanFill(MarkupKind kind) =>
        kind is MarkupKind.Rectangle or MarkupKind.Ellipse or MarkupKind.Polygon or MarkupKind.Cloud or MarkupKind.TextBox or MarkupKind.Area or MarkupKind.Callout;

    /// <summary>Messwerkzeuge der Mengenermittlung, dazu die Bemaßung (eine Länge mit Maßpfeilen).</summary>
    public static bool IsMeasurement(MarkupKind kind) =>
        kind is >= MarkupKind.Length and <= MarkupKind.Angle or MarkupKind.Dimension or MarkupKind.Radius or MarkupKind.Diameter;

    /// <summary>Messungen, deren Werte sich sinnvoll addieren lassen (nicht Winkel, Bemaßung, Radius, Durchmesser).</summary>
    public static bool IsSummable(MarkupKind kind) =>
        IsMeasurement(kind) && kind is not (MarkupKind.Angle or MarkupKind.Dimension or MarkupKind.Radius or MarkupKind.Diameter);

    /// <summary>Messungen, deren Wert vom Maßstab abhängt (nicht Anzahl und Winkel).</summary>
    public static bool NeedsScale(MarkupKind kind) => IsMeasurement(kind) && kind is not (MarkupKind.Count or MarkupKind.Angle);

    /// <summary>Mindestzahl an Punkten einer fertigen Markierung mit Einzelklicks.</summary>
    public static int MinimumPoints(MarkupKind kind) => kind switch
    {
        MarkupKind.Count => 1,
        MarkupKind.Angle => 3,
        _ => IsClosed(kind) ? 3 : 2,
    };

    public static bool HasText(MarkupKind kind) => kind is MarkupKind.TextBox or MarkupKind.Note or MarkupKind.Stamp or MarkupKind.Callout;

    /// <summary>Arten mit Textrahmen, der beim Bearbeiten des Textes nach unten mitwächst.</summary>
    public static bool HasTextFrame(MarkupKind kind) => kind is MarkupKind.TextBox or MarkupKind.Callout;

    /// <summary>Größe des Notizsymbols in Punkt.</summary>
    public const double NoteSize = 20;
}

/// <summary>Vorgefertigter Stempel: Aufschrift und Farbe. Eigene Aufschriften sind ebenfalls möglich.</summary>
public sealed record StampPreset(string Title, PdfColor Color)
{
    public static IReadOnlyList<StampPreset> Defaults { get; } =
    [
        new("GEPRÜFT", new PdfColor(30, 140, 60)),
        new("FREIGEGEBEN", new PdfColor(30, 140, 60)),
        new("FREIGEGEBEN MIT ANMERKUNGEN", new PdfColor(200, 120, 0)),
        new("ZUR PRÜFUNG", new PdfColor(200, 120, 0)),
        new("ABGELEHNT", new PdfColor(200, 30, 30)),
        new("ENTWURF", new PdfColor(90, 100, 115)),
        new("NUR ZUR INFORMATION", new PdfColor(22, 119, 210)),
    ];

    /// <summary>Standardgröße eines per Klick gesetzten Stempels in Punkt (Breite, Höhe auf dem Bildschirm).</summary>
    public static (double Width, double Height) DefaultSize => (190, 56);
}

/// <summary>Status einer Markierung: Bezeichnung, wer ihn gesetzt hat und wann.</summary>
public sealed record MarkupStatus(string Name, string By, DateTimeOffset At)
{
    /// <summary>
    /// Vorgegebene Status für Prüfvermerke (offen bis erledigt) und für Mengen (geprüft, freigegeben). Weitere
    /// Bezeichnungen aus anderen Dateien werden unverändert übernommen.
    /// </summary>
    public static IReadOnlyList<string> Names { get; } = ["Offen", "In Bearbeitung", "Erledigt", "Geprüft", "Freigegeben", "Abgelehnt"];
}
