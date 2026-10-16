namespace Egle360.Pdf;

/// <summary>Maßeinheiten. PDF rechnet in Punkt (1/72 Zoll).</summary>
public static class PdfUnits
{
    public const double MillimetersPerPoint = 25.4 / 72.0;

    public static double ToMillimeters(double points) => points * MillimetersPerPoint;

    public static double ToPoints(double millimeters) => millimeters / MillimetersPerPoint;
}

public enum PageOrientation
{
    Portrait,
    Landscape,
}

/// <summary>Papierformat einer Seite: ISO 216 A0 bis A4 oder benutzerdefiniert.</summary>
public sealed record PaperFormat(string Name, double WidthMm, double HeightMm, bool IsStandard)
{
    /// <summary>Abweichung, bis zu der eine Seite noch als Normformat gilt (Rundung beim Erzeugen von PDFs).</summary>
    public const double ToleranceMm = 2.0;

    private static readonly (string Name, double Short, double Long)[] Iso =
    [
        ("A0", 841, 1189),
        ("A1", 594, 841),
        ("A2", 420, 594),
        ("A3", 297, 420),
        ("A4", 210, 297),
    ];

    public PageOrientation Orientation => WidthMm > HeightMm ? PageOrientation.Landscape : PageOrientation.Portrait;

    /// <summary>Anzeigetext, z. B. „A1 quer, 841 × 594 mm“ oder „Benutzerdefiniert, 1000 × 707 mm“.</summary>
    public string Description
    {
        get
        {
            var size = $"{WidthMm:0} × {HeightMm:0} mm";
            if (!IsStandard)
            {
                return $"Benutzerdefiniert, {size}";
            }
            var orientation = Orientation == PageOrientation.Landscape ? "quer" : "hoch";
            return $"{Name} {orientation}, {size}";
        }
    }

    public static PaperFormat FromPoints(double widthPt, double heightPt)
    {
        var w = PdfUnits.ToMillimeters(widthPt);
        var h = PdfUnits.ToMillimeters(heightPt);
        var shortSide = Math.Min(w, h);
        var longSide = Math.Max(w, h);
        foreach (var (name, s, l) in Iso)
        {
            if (Math.Abs(shortSide - s) <= ToleranceMm && Math.Abs(longSide - l) <= ToleranceMm)
            {
                return new PaperFormat(name, w, h, true);
            }
        }
        return new PaperFormat("Benutzerdefiniert", w, h, false);
    }

    /// <summary>Größe eines Normformats in Punkt, hochkant.</summary>
    public static (double WidthPt, double HeightPt) IsoSizePoints(string name)
    {
        foreach (var (n, s, l) in Iso)
        {
            if (n == name)
            {
                return (PdfUnits.ToPoints(s), PdfUnits.ToPoints(l));
            }
        }
        throw new ArgumentException($"Unbekanntes Format {name}.", nameof(name));
    }
}

/// <summary>Seitengröße wie angezeigt, also mit angewandter Seitendrehung.</summary>
public sealed record PdfPageInfo(int Index, double WidthPt, double HeightPt, string? Label)
{
    public PaperFormat Format => PaperFormat.FromPoints(WidthPt, HeightPt);

    /// <summary>Seitenbezeichnung für die Anzeige: Seitenlabel aus dem PDF, sonst die laufende Nummer.</summary>
    public string DisplayLabel => string.IsNullOrWhiteSpace(Label) ? (Index + 1).ToString() : Label!;
}

public readonly record struct PdfColor(byte R, byte G, byte B, byte A = 255)
{
    public static readonly PdfColor Black = new(0, 0, 0);
    public static readonly PdfColor White = new(255, 255, 255);
}

public sealed record PdfBookmark(string Title, int? PageIndex, IReadOnlyList<PdfBookmark> Children);

public sealed record PdfMetadata(
    string? Title,
    string? Author,
    string? Subject,
    string? Keywords,
    string? Creator,
    string? Producer,
    DateTimeOffset? Created,
    DateTimeOffset? Modified);

/// <summary>Berechtigungsbits nach PDF 32000-1, Tabelle 22. Ohne Verschlüsselung sind alle gesetzt.</summary>
[Flags]
public enum PdfPermissions : uint
{
    None = 0,
    Print = 1 << 2,
    Modify = 1 << 3,
    Copy = 1 << 4,
    Annotate = 1 << 5,
    FillForms = 1 << 8,
    Extract = 1 << 9,
    Assemble = 1 << 10,
    PrintHighQuality = 1 << 11,
}

public enum PdfSaveMode
{
    /// <summary>Hängt nur die Änderungen an. Die ursprünglichen Bytes bleiben unverändert erhalten.</summary>
    Incremental,

    /// <summary>Schreibt das Dokument neu. Entfernte Seiten sind danach nicht mehr in der Datei enthalten.</summary>
    Rewrite,
}

/// <summary>Ausschnitt in Pixeln innerhalb der vollständig gerenderten Seite.</summary>
public readonly record struct PixelRect(int X, int Y, int Width, int Height);
