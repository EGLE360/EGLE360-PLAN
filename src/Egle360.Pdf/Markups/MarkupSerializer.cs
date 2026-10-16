using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Egle360.Pdf.Markups;

/// <summary>
/// Übersetzt Markierungen in die Bestandteile einer PDF-Anmerkung: Erscheinungsstrom, EGLE360-Daten (JSON) und die
/// Standardschlüssel, die PDFium nicht direkt setzen kann.
/// </summary>
public static class MarkupSerializer
{
    /// <summary>Privater Schlüssel mit den vollständigen EGLE360-Daten der Markierung (JSON, Version 1).</summary>
    public const string DataKey = "E360Markup";

    /// <summary>
    /// Privater Schlüssel mit Standardeinträgen (z. B. /Subtype /Line /L [...]), die beim Speichern in die Anmerkung
    /// übernommen werden. Siehe <see cref="PdfObjectPatcher"/>.
    /// </summary>
    public const string StandardKey = "E360Std";

    private static readonly JsonSerializerOptions Json = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    // ---------- Erscheinungsstrom ----------

    /// <summary>
    /// Inhaltsstrom der Normaldarstellung (AP /N). Koordinaten sind Benutzerkoordinaten, weil PDFium BBox gleich Rect
    /// setzt und keine Matrix einträgt. Bei Deckkraft unter 1 legt PDFium den Grafikzustand /GS an.
    /// </summary>
    public static string AppearanceStream(MarkupShape shape)
    {
        var sb = new StringBuilder();
        sb.Append("q\n");
        if (shape.Opacity < 1)
        {
            sb.Append("/GS gs\n");
        }
        sb.Append("1 J 1 j\n");
        foreach (var layer in shape.Layers)
        {
            if (layer.Path.IsEmpty || (layer.Stroke is null && layer.Fill is null))
            {
                continue;
            }
            sb.Append("q\n");
            if (layer.Clip is { } clip)
            {
                sb.Append($"{N(clip.Left)} {N(clip.Bottom)} {N(clip.Width)} {N(clip.Height)} re W n\n");
            }
            if (layer.Fill is { } f)
            {
                sb.Append($"{C(f.R)} {C(f.G)} {C(f.B)} rg\n");
            }
            if (layer.Stroke is { } s)
            {
                sb.Append($"{C(s.R)} {C(s.G)} {C(s.B)} RG {N(layer.StrokeWidth)} w\n");
            }
            AppendPath(sb, layer.Path);
            sb.Append(layer switch
            {
                { Fill: not null, Stroke: not null } => "B\n",
                { Fill: not null } => "f\n",
                _ => "S\n",
            });
            sb.Append("Q\n");
        }
        sb.Append("Q\n");
        return sb.ToString();
    }

    private static void AppendPath(StringBuilder sb, PathData path)
    {
        foreach (var f in path.Figures)
        {
            sb.Append($"{N(f.Start.X)} {N(f.Start.Y)} m\n");
            foreach (var s in f.Segments)
            {
                if (s.Kind == SegmentKind.Line)
                {
                    sb.Append($"{N(s.End.X)} {N(s.End.Y)} l\n");
                }
                else
                {
                    sb.Append($"{N(s.C1.X)} {N(s.C1.Y)} {N(s.C2.X)} {N(s.C2.Y)} {N(s.End.X)} {N(s.End.Y)} c\n");
                }
            }
            if (f.Closed)
            {
                sb.Append("h\n");
            }
        }
    }

    /// <summary>Zahl im PDF-Format: höchstens drei Nachkommastellen, ohne Exponent.</summary>
    public static string N(double v)
    {
        var r = Math.Round(v, 3);
        return (r == 0 ? 0 : r).ToString("0.###", CultureInfo.InvariantCulture);
    }

    private static string C(byte v) => N(v / 255.0);

    // ---------- Standardschlüssel ----------

    /// <summary>PDFium kann diese Anmerkungsarten nicht anlegen; sie entstehen als Ink und werden beim Speichern umgeschrieben.</summary>
    public static bool NeedsStandardPatch(MarkupKind kind) =>
        kind is MarkupKind.Line or MarkupKind.Arrow or MarkupKind.Polyline or MarkupKind.Polygon or MarkupKind.Cloud;

    /// <summary>Einträge, die beim Speichern in die Anmerkung geschrieben werden; null, wenn keine nötig sind.</summary>
    public static string? StandardEntries(Markup m)
    {
        var pts = m.Points;
        string Vertices() => "[" + string.Join(" ", pts.Select(p => $"{N(p.X)} {N(p.Y)}")) + "]";
        return m.Kind switch
        {
            MarkupKind.Line when pts.Count >= 2 => $"<</Subtype /Line /L [{N(pts[0].X)} {N(pts[0].Y)} {N(pts[1].X)} {N(pts[1].Y)}]>>",
            MarkupKind.Arrow when pts.Count >= 2 => $"<</Subtype /Line /L [{N(pts[0].X)} {N(pts[0].Y)} {N(pts[1].X)} {N(pts[1].Y)}] /LE [/None /OpenArrow]>>",
            MarkupKind.Polyline => $"<</Subtype /PolyLine /Vertices {Vertices()}>>",
            MarkupKind.Polygon => $"<</Subtype /Polygon /Vertices {Vertices()}>>",
            MarkupKind.Cloud => $"<</Subtype /Polygon /Vertices {Vertices()} /BE <</S /C /I 1>>>>",
            _ => null,
        };
    }

    /// <summary>Freitext-Standardformat (DA) für Programme, die die Darstellung selbst erzeugen.</summary>
    public static string DefaultAppearance(Markup m) =>
        $"{C(m.Style.Stroke.R)} {C(m.Style.Stroke.G)} {C(m.Style.Stroke.B)} rg /Helv {N(m.Style.FontSize)} Tf";

    /// <summary>PDF-Datumsformat D:YYYYMMDDHHmmSS+HH'mm'.</summary>
    public static string PdfDate(DateTimeOffset d)
    {
        var o = d.Offset;
        var sign = o < TimeSpan.Zero ? '-' : '+';
        o = o.Duration();
        return $"D:{d:yyyyMMddHHmmss}{sign}{o.Hours:00}'{o.Minutes:00}'";
    }

    // ---------- EGLE360-Daten ----------

    public static string ToJson(Markup m)
    {
        var dto = new Dto
        {
            V = 1,
            Kind = m.Kind,
            Paths = m.Paths.Select(p => p.SelectMany(q => new[] { Math.Round(q.X, 3), Math.Round(q.Y, 3) }).ToArray()).ToArray(),
            Stroke = Hex(m.Style.Stroke),
            Fill = m.Style.Fill is { } f ? Hex(f) : null,
            Opacity = Math.Round(m.Style.Opacity, 3),
            Width = Math.Round(m.Style.LineWidth, 3),
            Font = Math.Round(m.Style.FontSize, 3),
            Rot = m.TextRotation,
            Created = m.Created,
        };
        return JsonSerializer.Serialize(dto, Json);
    }

    /// <summary>Liest die EGLE360-Daten. Text, Autor, Name und Änderungsdatum kommen aus den Standardschlüsseln der Anmerkung.</summary>
    public static Markup? FromJson(string json, string id, string text, string author, DateTimeOffset? modified)
    {
        try
        {
            var dto = JsonSerializer.Deserialize<Dto>(json, Json);
            if (dto is null || dto.V != 1 || dto.Paths is null || !Enum.IsDefined(dto.Kind))
            {
                return null;
            }
            var paths = dto.Paths
                .Where(a => a.Length % 2 == 0)
                .Select(a => (IReadOnlyList<PdfPoint>)Enumerable.Range(0, a.Length / 2).Select(i => new PdfPoint(a[2 * i], a[2 * i + 1])).ToArray())
                .ToArray();
            if (paths.All(p => p.Count == 0) || paths.SelectMany(p => p).Any(p => !double.IsFinite(p.X) || !double.IsFinite(p.Y)))
            {
                return null;
            }
            var style = new MarkupStyle(ParseHex(dto.Stroke) ?? PdfColor.Black, ParseHex(dto.Fill), Math.Clamp(dto.Opacity, 0, 1), Math.Max(0, dto.Width), dto.Font > 0 ? dto.Font : 12);
            var created = dto.Created ?? modified ?? DateTimeOffset.Now;
            return new Markup(id, dto.Kind, paths, style, text, author, created, modified ?? created, TextFrame.Normalize(dto.Rot));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Hex(PdfColor c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

    private static PdfColor? ParseHex(string? s) =>
        s is { Length: 6 } && int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v)
            ? new PdfColor((byte)(v >> 16), (byte)(v >> 8), (byte)v)
            : null;

    private sealed class Dto
    {
        [JsonPropertyName("v")] public int V { get; set; }
        [JsonPropertyName("kind")] public MarkupKind Kind { get; set; }
        [JsonPropertyName("paths")] public double[][]? Paths { get; set; }
        [JsonPropertyName("stroke")] public string? Stroke { get; set; }
        [JsonPropertyName("fill")] public string? Fill { get; set; }
        [JsonPropertyName("opacity")] public double Opacity { get; set; } = 1;
        [JsonPropertyName("width")] public double Width { get; set; } = 1;
        [JsonPropertyName("font")] public double Font { get; set; } = 12;
        [JsonPropertyName("rot")] public int Rot { get; set; }
        [JsonPropertyName("created")] public DateTimeOffset? Created { get; set; }
    }
}
