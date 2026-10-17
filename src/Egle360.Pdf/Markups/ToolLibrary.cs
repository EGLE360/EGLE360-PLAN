using System.Text.Json;
using System.Text.Json.Serialization;

namespace Egle360.Pdf.Markups;

/// <summary>
/// Gespeichertes Werkzeug: Markierungsart mit Darstellung und Vorgaben, die jede damit gezeichnete Markierung erhält.
/// </summary>
/// <param name="Id">Eindeutiger Schlüssel, bleibt beim Umbenennen gleich.</param>
/// <param name="Name">Anzeigename, z. B. „Bodenbelag Fliesen“.</param>
/// <param name="Text">Aufschrift (Stempel, Textfeld) oder Kommentar, den neue Markierungen bekommen.</param>
/// <param name="Depth">Höhe oder Tiefe in Metern für Flächen, Längen und Umfänge.</param>
/// <param name="UnitPrice">Einheitspreis in Euro für Messungen.</param>
public sealed record ToolPreset(string Id, string Name, MarkupKind Kind, MarkupStyle Style, string Text = "", double? Depth = null,
    double? UnitPrice = null, bool Favorite = false)
{
    public static string NewId() => "tool-" + Guid.NewGuid().ToString("N");

    /// <summary>Werkzeug aus einer vorhandenen Markierung: Art, Darstellung, Text, Höhe und Preis werden übernommen.</summary>
    public static ToolPreset FromMarkup(Markup m, string name) =>
        new(NewId(), name, m.Kind, m.Style, m.Text, Measurement.SupportsDepth(m.Kind) ? m.Depth : null,
            MarkupKinds.IsSummable(m.Kind) ? m.UnitPrice : null);

    /// <summary>Überträgt die Vorgaben auf eine eben gezeichnete Markierung derselben Art.</summary>
    public Markup ApplyTo(Markup m)
    {
        if (m.Kind != Kind)
        {
            return m;
        }
        return m with
        {
            Style = Style,
            Text = m.Text.Length > 0 ? m.Text : Text,
            Depth = Measurement.SupportsDepth(Kind) ? Depth ?? m.Depth : m.Depth,
            UnitPrice = MarkupKinds.IsSummable(Kind) ? UnitPrice ?? m.UnitPrice : m.UnitPrice,
        };
    }

    /// <summary>Kurzbeschreibung für Listen, z. B. „Fläche, Tiefe 0,20 m, 38,50 € je m²“.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string> { MarkupKinds.DisplayName(Kind) };
            if (Depth is { } d)
            {
                parts.Add($"{(Kind == MarkupKind.Area ? "Tiefe" : "Höhe")} {Measurement.Number(d, 2)} m");
            }
            if (UnitPrice is { } p)
            {
                parts.Add($"{Measurement.Number(p, 2)} €");
            }
            return string.Join(", ", parts);
        }
    }
}

/// <summary>Benannte Gruppe von Werkzeugen, z. B. „Mengen Innenausbau“.</summary>
public sealed record ToolSet(string Name, IReadOnlyList<ToolPreset> Tools);

/// <summary>
/// Werkzeugbibliothek: alle Werkzeugsätze eines Benutzers. Gespeichert als JSON (Format „e360-tools“, Version 1),
/// damit Sätze zwischen Arbeitsplätzen ausgetauscht werden können.
/// </summary>
public sealed record ToolLibrary(IReadOnlyList<ToolSet> Sets)
{
    public const string Format = "e360-tools";

    public static ToolLibrary Empty { get; } = new([]);

    public IEnumerable<ToolPreset> AllTools => Sets.SelectMany(s => s.Tools);

    public ToolPreset? Find(string id) => AllTools.FirstOrDefault(t => t.Id == id);

    /// <summary>Werkzeuge, deren Name, Satz oder Art den Suchtext enthält (Groß- und Kleinschreibung egal). Favoriten zuerst.</summary>
    public IReadOnlyList<(ToolSet Set, ToolPreset Tool)> Search(string query)
    {
        var q = query.Trim();
        return Sets.SelectMany(s => s.Tools.Select(t => (Set: s, Tool: t)))
            .Where(x => q.Length == 0
                || x.Tool.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || x.Set.Name.Contains(q, StringComparison.CurrentCultureIgnoreCase)
                || MarkupKinds.DisplayName(x.Tool.Kind).Contains(q, StringComparison.CurrentCultureIgnoreCase))
            .OrderByDescending(x => x.Tool.Favorite)
            .ToList();
    }

    /// <summary>Fügt ein Werkzeug in den Satz <paramref name="setName"/> ein; fehlt der Satz, wird er angelegt.</summary>
    public ToolLibrary Add(string setName, ToolPreset tool)
    {
        var sets = Sets.ToList();
        var i = sets.FindIndex(s => s.Name == setName);
        if (i < 0)
        {
            sets.Add(new ToolSet(setName, [tool]));
        }
        else
        {
            sets[i] = sets[i] with { Tools = [.. sets[i].Tools, tool] };
        }
        return new ToolLibrary(sets);
    }

    /// <summary>Ersetzt ein Werkzeug mit derselben Id.</summary>
    public ToolLibrary Replace(ToolPreset tool) =>
        new([.. Sets.Select(s => s with { Tools = [.. s.Tools.Select(t => t.Id == tool.Id ? tool : t)] })]);

    public ToolLibrary Remove(string id) =>
        new([.. Sets.Select(s => s with { Tools = [.. s.Tools.Where(t => t.Id != id)] })]);

    public ToolLibrary RemoveSet(string name) => new([.. Sets.Where(s => s.Name != name)]);

    public ToolLibrary RenameSet(string name, string newName) =>
        new([.. Sets.Select(s => s.Name == name ? s with { Name = newName } : s)]);

    /// <summary>
    /// Übernimmt Sätze aus einer anderen Bibliothek. Gleichnamige Sätze werden ergänzt; Werkzeuge mit schon
    /// vorhandener Id werden übersprungen, damit ein zweiter Import nichts verdoppelt.
    /// </summary>
    public ToolLibrary Merge(ToolLibrary other)
    {
        var result = this;
        var known = AllTools.Select(t => t.Id).ToHashSet();
        foreach (var set in other.Sets)
        {
            if (result.Sets.All(s => s.Name != set.Name))
            {
                result = new ToolLibrary([.. result.Sets, new ToolSet(set.Name, [])]);
            }
            foreach (var tool in set.Tools.Where(t => known.Add(t.Id)))
            {
                result = result.Add(set.Name, tool);
            }
        }
        return result;
    }

    /// <summary>
    /// Mitgelieferte Grundausstattung. Nur Darstellung und Art, keine Preise oder Höhen: Die hängen vom Projekt ab
    /// und werden vom Benutzer ergänzt.
    /// </summary>
    public static ToolLibrary Defaults()
    {
        var red = new PdfColor(200, 30, 30);
        var blue = new PdfColor(22, 119, 210);
        var green = new PdfColor(30, 140, 60);
        var orange = new PdfColor(230, 120, 0);
        var purple = new PdfColor(130, 60, 170);
        MarkupStyle Measure(PdfColor c, bool fill = false) =>
            new(c, fill ? new PdfColor((byte)(c.R * 0.2 + 204), (byte)(c.G * 0.2 + 204), (byte)(c.B * 0.2 + 204)) : null, fill ? 0.75 : 1, 1.5, 10);
        ToolPreset T(string name, MarkupKind kind, MarkupStyle style, string text = "") => new("default-" + Slug(name), name, kind, style, text);
        return new ToolLibrary(
        [
            new ToolSet("Planprüfung",
            [
                T("Mangel", MarkupKind.Cloud, new MarkupStyle(red, null, 1, 1.5)),
                T("Hinweis", MarkupKind.Callout, MarkupStyle.DefaultFor(MarkupKind.Callout)),
                T("Rückfrage", MarkupKind.Cloud, new MarkupStyle(orange, null, 1, 1.5)),
                T("Erledigt", MarkupKind.Rectangle, new MarkupStyle(green, null, 1, 1.5)),
                T("Geprüft", MarkupKind.Stamp, MarkupStyle.DefaultFor(MarkupKind.Stamp) with { Stroke = StampPreset.Defaults[0].Color }, StampPreset.Defaults[0].Title),
            ]),
            new ToolSet("Mengen Hochbau",
            [
                T("Bodenfläche", MarkupKind.Area, Measure(blue, fill: true)),
                T("Deckenfläche", MarkupKind.Area, Measure(purple, fill: true)),
                T("Wand (Länge mal Höhe)", MarkupKind.Polylength, Measure(orange)),
                T("Sockelleiste", MarkupKind.Polylength, Measure(green)),
                T("Türen", MarkupKind.Count, Measure(red)),
                T("Fenster", MarkupKind.Count, Measure(blue)),
            ]),
        ]);
    }

    private static string Slug(string s) =>
        new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray()).Trim('-');

    // ---------- Speichern und Laden ----------

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public string ToJson() => JsonSerializer.Serialize(new FileDto
    {
        Format = Format,
        Version = 1,
        Sets = [.. Sets.Select(s => new SetDto
        {
            Name = s.Name,
            Tools = [.. s.Tools.Select(t => new ToolDto
            {
                Id = t.Id, Name = t.Name, Kind = t.Kind.ToString(), Text = t.Text.Length > 0 ? t.Text : null,
                Stroke = MarkupSerializer.Hex(t.Style.Stroke), Fill = t.Style.Fill is { } f ? MarkupSerializer.Hex(f) : null,
                Opacity = t.Style.Opacity, Width = t.Style.LineWidth, Font = t.Style.FontSize,
                Depth = t.Depth, Price = t.UnitPrice, Favorite = t.Favorite ? true : null,
            })],
        })],
    }, Json);

    /// <summary>Liest eine Bibliothek. Ungültige Werkzeuge werden übersprungen; eine fremde oder kaputte Datei ergibt eine Ausnahme.</summary>
    public static ToolLibrary FromJson(string json)
    {
        FileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<FileDto>(json, Json);
        }
        catch (JsonException e)
        {
            throw new FormatException("Die Datei ist keine EGLE360-Werkzeugbibliothek.", e);
        }
        if (dto is null || dto.Format != Format || dto.Version != 1)
        {
            throw new FormatException("Die Datei ist keine EGLE360-Werkzeugbibliothek.");
        }
        var sets = new List<ToolSet>();
        foreach (var s in dto.Sets ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Name))
            {
                continue;
            }
            var tools = (s.Tools ?? [])
                .Where(t => !string.IsNullOrWhiteSpace(t.Id) && !string.IsNullOrWhiteSpace(t.Name)
                    && Enum.TryParse<MarkupKind>(t.Kind, out var k) && Enum.IsDefined(k) && !int.TryParse(t.Kind, out _))
                .Select(t => new ToolPreset(t.Id!, t.Name!.Trim(), Enum.Parse<MarkupKind>(t.Kind!),
                    new MarkupStyle(MarkupSerializer.ParseHex(t.Stroke) ?? PdfColor.Black, MarkupSerializer.ParseHex(t.Fill),
                        Math.Clamp(t.Opacity, 0, 1), Math.Clamp(t.Width, 0, 100), t.Font is > 0 and < 500 ? t.Font : 12),
                    t.Text ?? "", t.Depth is > 0 and < 1e6 ? t.Depth : null, t.Price is >= 0 and < 1e9 ? t.Price : null, t.Favorite == true))
                .ToList();
            sets.Add(new ToolSet(s.Name.Trim(), tools));
        }
        return new ToolLibrary(sets);
    }

    private sealed class FileDto
    {
        [JsonPropertyName("format")] public string? Format { get; set; }
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("sets")] public List<SetDto>? Sets { get; set; }
    }

    private sealed class SetDto
    {
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("tools")] public List<ToolDto>? Tools { get; set; }
    }

    private sealed class ToolDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("kind")] public string? Kind { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
        [JsonPropertyName("stroke")] public string? Stroke { get; set; }
        [JsonPropertyName("fill")] public string? Fill { get; set; }
        [JsonPropertyName("opacity")] public double Opacity { get; set; } = 1;
        [JsonPropertyName("width")] public double Width { get; set; } = 1.5;
        [JsonPropertyName("font")] public double Font { get; set; } = 12;
        [JsonPropertyName("depth")] public double? Depth { get; set; }
        [JsonPropertyName("price")] public double? Price { get; set; }
        [JsonPropertyName("favorite")] public bool? Favorite { get; set; }
    }
}
