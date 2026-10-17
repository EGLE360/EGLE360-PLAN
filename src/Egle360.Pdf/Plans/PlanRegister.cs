using System.Text.Json;
using System.Text.Json.Serialization;

namespace Egle360.Pdf.Plans;

/// <summary>Ein Planstand (Index) mit seiner Datei im Planprojekt.</summary>
/// <param name="Index">Planindex, z. B. „A“, „B“ oder „03“; leer beim Erststand ohne Index.</param>
/// <param name="File">Pfad relativ zum Projektordner, mit „/“ getrennt.</param>
/// <param name="Note">Änderungsvermerk zum Index.</param>
public sealed record PlanRevision(string Index, string File, DateTimeOffset Added, string AddedBy, DateOnly? Date = null, string Note = "");

/// <summary>Ein Plan der Planliste mit allen Ständen. Der zuletzt hinzugefügte Stand ist der aktuelle.</summary>
public sealed record PlanEntry(
    string Id,
    string Number,
    string Title,
    IReadOnlyList<PlanRevision> Revisions,
    string Discipline = "",
    string Floor = "",
    string Building = "",
    string Section = "",
    string Status = "",
    string Author = "",
    string Approval = "")
{
    public PlanRevision Current => Revisions[^1];

    public string Index => Current.Index;

    public static string NewId() => "plan-" + Guid.NewGuid().ToString("N");
}

/// <summary>Gewerk oder Fachrichtung mit Kürzel für die Planliste.</summary>
public sealed record PlanDiscipline(string Code, string Name)
{
    /// <summary>
    /// Vorschlag für die Auswahlliste. Die Kürzel sind in Projekten nicht einheitlich geregelt und können in der
    /// Planliste frei eingegeben werden.
    /// </summary>
    public static IReadOnlyList<PlanDiscipline> Defaults { get; } =
    [
        new("AR", "Architektur"),
        new("TW", "Tragwerksplanung"),
        new("HZ", "Heizung"),
        new("LÜ", "Lüftung"),
        new("SA", "Sanitär"),
        new("EL", "Elektro"),
        new("BS", "Brandschutz"),
        new("BP", "Bauphysik"),
        new("FR", "Freianlagen"),
        new("TB", "Tiefbau"),
        new("VM", "Vermessung"),
        new("IA", "Innenausbau"),
    ];
}

/// <summary>Planliste eines Planprojekts, gespeichert als JSON (Format „e360-plans“, Version 1).</summary>
public sealed record PlanRegister(string Name, IReadOnlyList<PlanEntry> Plans)
{
    public const string Format = "e360-plans";

    public const string FileName = "Planliste.e360plans.json";

    /// <summary>Übliche Planstatus; frei erweiterbar.</summary>
    public static IReadOnlyList<string> StatusNames { get; } = ["Entwurf", "Zur Prüfung", "Zur Ausführung", "Freigegeben", "Ungültig"];

    public PlanEntry? Find(string id) => Plans.FirstOrDefault(p => p.Id == id);

    /// <summary>Plan mit derselben Plannummer (ohne Groß- und Kleinschreibung, Leerzeichen und Trennzeichen-Unterschiede).</summary>
    public PlanEntry? FindByNumber(string number)
    {
        var key = NumberKey(number);
        return key.Length == 0 ? null : Plans.FirstOrDefault(p => NumberKey(p.Number) == key);
    }

    /// <summary>Vergleichsschlüssel einer Plannummer: „GR-EG-01“, „gr_eg_01“ und „GR EG 01“ gelten als gleich.</summary>
    public static string NumberKey(string number) =>
        new string(number.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public PlanRegister Add(PlanEntry plan) => this with { Plans = [.. Plans, plan] };

    public PlanRegister Replace(PlanEntry plan) => this with { Plans = [.. Plans.Select(p => p.Id == plan.Id ? plan : p)] };

    public PlanRegister Remove(string id) => this with { Plans = [.. Plans.Where(p => p.Id != id)] };

    /// <summary>Pläne, deren Felder den Suchtext enthalten, sortiert nach Plannummer.</summary>
    public IReadOnlyList<PlanEntry> Filter(string query, string discipline = "")
    {
        var q = query.Trim();
        return Plans
            .Where(p => discipline.Length == 0 || p.Discipline == discipline)
            .Where(p => q.Length == 0 || new[] { p.Number, p.Title, p.Index, p.Discipline, p.Floor, p.Building, p.Section, p.Status, p.Author }
                .Any(f => f.Contains(q, StringComparison.CurrentCultureIgnoreCase)))
            .OrderBy(p => p.Number, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("de-DE"), System.Globalization.CompareOptions.NumericOrdering))
            .ToList();
    }

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
        Name = Name,
        Plans = [.. Plans.Select(p => new PlanDto
        {
            Id = p.Id, Number = p.Number, Title = p.Title, Discipline = Empty(p.Discipline), Floor = Empty(p.Floor), Building = Empty(p.Building),
            Section = Empty(p.Section), Status = Empty(p.Status), Author = Empty(p.Author), Approval = Empty(p.Approval),
            Revisions = [.. p.Revisions.Select(r => new RevisionDto
            {
                Index = r.Index, File = r.File, Added = r.Added, AddedBy = Empty(r.AddedBy), Date = r.Date?.ToString("yyyy-MM-dd"), Note = Empty(r.Note),
            })],
        })],
    }, Json);

    private static string? Empty(string s) => s.Length == 0 ? null : s;

    public static PlanRegister FromJson(string json)
    {
        FileDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<FileDto>(json, Json);
        }
        catch (JsonException e)
        {
            throw new FormatException("Die Datei ist keine EGLE360-Planliste.", e);
        }
        if (dto is null || dto.Format != Format || dto.Version != 1)
        {
            throw new FormatException("Die Datei ist keine EGLE360-Planliste.");
        }
        var plans = new List<PlanEntry>();
        foreach (var p in dto.Plans ?? [])
        {
            var revisions = (p.Revisions ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.File) && IsSafeRelative(r.File!))
                .Select(r => new PlanRevision(r.Index ?? "", r.File!, r.Added ?? DateTimeOffset.MinValue, r.AddedBy ?? "",
                    DateOnly.TryParseExact(r.Date, "yyyy-MM-dd", out var d) ? d : null, r.Note ?? ""))
                .ToList();
            if (string.IsNullOrWhiteSpace(p.Id) || revisions.Count == 0)
            {
                continue;
            }
            plans.Add(new PlanEntry(p.Id!, p.Number ?? "", p.Title ?? "", revisions, p.Discipline ?? "", p.Floor ?? "", p.Building ?? "",
                p.Section ?? "", p.Status ?? "", p.Author ?? "", p.Approval ?? ""));
        }
        return new PlanRegister(dto.Name ?? "", plans);
    }

    /// <summary>Nur Pfade innerhalb des Projektordners: relativ, ohne „..“ und ohne Laufwerk.</summary>
    public static bool IsSafeRelative(string path) =>
        !Path.IsPathRooted(path) && !path.Contains(':') && path.Split('/', '\\').All(part => part is not ".." and not "");

    private sealed class FileDto
    {
        [JsonPropertyName("format")] public string? Format { get; set; }
        [JsonPropertyName("version")] public int Version { get; set; }
        [JsonPropertyName("name")] public string? Name { get; set; }
        [JsonPropertyName("plans")] public List<PlanDto>? Plans { get; set; }
    }

    private sealed class PlanDto
    {
        [JsonPropertyName("id")] public string? Id { get; set; }
        [JsonPropertyName("number")] public string? Number { get; set; }
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("discipline")] public string? Discipline { get; set; }
        [JsonPropertyName("floor")] public string? Floor { get; set; }
        [JsonPropertyName("building")] public string? Building { get; set; }
        [JsonPropertyName("section")] public string? Section { get; set; }
        [JsonPropertyName("status")] public string? Status { get; set; }
        [JsonPropertyName("author")] public string? Author { get; set; }
        [JsonPropertyName("approval")] public string? Approval { get; set; }
        [JsonPropertyName("revisions")] public List<RevisionDto>? Revisions { get; set; }
    }

    private sealed class RevisionDto
    {
        [JsonPropertyName("index")] public string? Index { get; set; }
        [JsonPropertyName("file")] public string? File { get; set; }
        [JsonPropertyName("added")] public DateTimeOffset? Added { get; set; }
        [JsonPropertyName("addedBy")] public string? AddedBy { get; set; }
        [JsonPropertyName("date")] public string? Date { get; set; }
        [JsonPropertyName("note")] public string? Note { get; set; }
    }
}
