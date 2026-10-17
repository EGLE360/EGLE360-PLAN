using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Plans;

/// <summary>Ergebnis beim Hinzufügen einer Datei zum Planprojekt.</summary>
public enum PlanImportAction
{
    /// <summary>Neuer Plan angelegt.</summary>
    NewPlan,

    /// <summary>Als neuer Index eines vorhandenen Plans eingetragen.</summary>
    NewRevision,

    /// <summary>Übersprungen, weil der Plan diesen Index schon hat.</summary>
    Duplicate,

    /// <summary>Nicht lesbar (kein PDF, beschädigt, kennwortgeschützt).</summary>
    Failed,
}

public sealed record PlanImportResult(string Source, PlanImportAction Action, string PlanId = "", int MarkupsTakenOver = 0, string Message = "");

/// <summary>
/// Planprojekt: ein Ordner mit der Planliste und den Plandateien. Hinzugefügte Pläne werden in den Unterordner
/// „Pläne/&lt;Plannummer&gt;“ kopiert; die Originale bleiben unverändert. Jeder Index behält seine eigene Datei, frühere
/// Stände bleiben also erhalten und lassen sich jederzeit öffnen.
/// </summary>
public sealed class PlanProject
{
    public const string PlanFolder = "Pläne";

    private PlanProject(string root, PlanRegister register)
    {
        Root = Path.GetFullPath(root);
        Register = register;
    }

    public string Root { get; }

    public PlanRegister Register { get; private set; }

    public string RegisterPath => Path.Combine(Root, PlanRegister.FileName);

    public static bool Exists(string root) => File.Exists(Path.Combine(root, PlanRegister.FileName));

    public static PlanProject Create(string root, string name)
    {
        Directory.CreateDirectory(root);
        if (Exists(root))
        {
            throw new IOException("In diesem Ordner gibt es schon eine Planliste.");
        }
        var project = new PlanProject(root, new PlanRegister(name, []));
        project.Save();
        return project;
    }

    public static PlanProject Open(string root) =>
        new(root, PlanRegister.FromJson(File.ReadAllText(Path.Combine(root, PlanRegister.FileName))));

    /// <summary>Übernimmt eine geänderte Planliste und speichert sie.</summary>
    public void Update(PlanRegister register)
    {
        Register = register;
        Save();
    }

    private void Save()
    {
        var tmp = RegisterPath + ".tmp";
        File.WriteAllText(tmp, Register.ToJson());
        File.Move(tmp, RegisterPath, overwrite: true);
    }

    public string FullPath(PlanRevision revision) => Path.Combine(Root, revision.File.Replace('/', Path.DirectorySeparatorChar));

    /// <summary>Liest Plannummer, Index und Bezeichnung aus der ersten Seite und dem Dateinamen; null bei unlesbarer Datei.</summary>
    public static PlanInfo? ReadInfo(string pdfPath)
    {
        try
        {
            using var doc = PdfDocument.Open(pdfPath);
            return PlanInfoReader.Read(pdfPath, doc.PageCount > 0 ? doc.GetPageText(0) : "");
        }
        catch (PdfException)
        {
            return null;
        }
    }

    /// <summary>
    /// Fügt Dateien hinzu. Gehört eine Datei nach ihrer Plannummer zu einem vorhandenen Plan und hat sie einen neuen
    /// Index, wird sie als neuer Stand eingetragen; auf Wunsch werden die Markierungen des bisherigen Stands übernommen.
    /// </summary>
    public IReadOnlyList<PlanImportResult> Import(IEnumerable<string> files, string user, bool takeOverMarkups, ITextOutliner outliner)
    {
        var results = new List<PlanImportResult>();
        foreach (var file in files)
        {
            if (ReadInfo(file) is not { } info)
            {
                results.Add(new PlanImportResult(file, PlanImportAction.Failed, Message: "Die Datei ist kein lesbares PDF oder ist kennwortgeschützt."));
                continue;
            }
            if (Register.FindByNumber(info.Number) is { } existing)
            {
                if (existing.Revisions.Any(r => string.Equals(r.Index, info.Index, StringComparison.OrdinalIgnoreCase)))
                {
                    results.Add(new PlanImportResult(file, PlanImportAction.Duplicate, existing.Id,
                        Message: $"Plan {existing.Number} hat den Index {Display(info.Index)} schon."));
                    continue;
                }
                var (_, taken) = AddRevision(existing.Id, file, info.Index, user, "", takeOverMarkups, outliner);
                results.Add(new PlanImportResult(file, PlanImportAction.NewRevision, existing.Id, taken));
                continue;
            }
            var plan = AddPlan(file, info, user);
            results.Add(new PlanImportResult(file, PlanImportAction.NewPlan, plan.Id));
        }
        return results;
    }

    public static string Display(string index) => index.Length == 0 ? "ohne Index" : index;

    /// <summary>Legt einen neuen Plan mit einer Kopie der Datei an.</summary>
    public PlanEntry AddPlan(string source, PlanInfo info, string user)
    {
        var number = info.Number.Length > 0 ? info.Number : Path.GetFileNameWithoutExtension(source);
        var relative = CopyIn(source, number);
        var plan = new PlanEntry(PlanEntry.NewId(), number, info.Title, [new PlanRevision(info.Index, relative, DateTimeOffset.Now, user)]);
        Update(Register.Add(plan));
        return plan;
    }

    /// <summary>
    /// Trägt einen neuen Index ein. Mit <paramref name="takeOverMarkups"/> werden die Markierungen und Maßstäbe des
    /// bisherigen Stands seitenweise in die Kopie der neuen Datei übernommen (Seiten, die es dort nicht gibt, entfallen).
    /// </summary>
    public (PlanEntry Plan, int MarkupsTakenOver) AddRevision(string planId, string source, string index, string user, string note,
        bool takeOverMarkups, ITextOutliner outliner)
    {
        var plan = Register.Find(planId) ?? throw new ArgumentException("Plan nicht gefunden.", nameof(planId));
        var previous = plan.Current;
        var relative = CopyIn(source, plan.Number);
        var taken = takeOverMarkups ? CopyMarkups(FullPath(previous), Path.Combine(Root, relative), outliner) : 0;
        var updated = plan with { Revisions = [.. plan.Revisions, new PlanRevision(index, relative, DateTimeOffset.Now, user, Note: note)] };
        Update(Register.Replace(updated));
        return (updated, taken);
    }

    /// <summary>Überträgt Markierungen und Seitenmaßstäbe von einer Datei in eine andere, Seite für Seite.</summary>
    public static int CopyMarkups(string from, string to, ITextOutliner outliner)
    {
        using var source = PdfDocument.Open(from);
        using var target = PdfDocument.Open(to);
        if (!target.CanAnnotate)
        {
            return 0;
        }
        var count = 0;
        for (var page = 0; page < Math.Min(source.PageCount, target.PageCount); page++)
        {
            var markups = source.GetMarkups(page);
            if (source.GetPageScale(page) is { } scale && target.GetPageScale(page) is null)
            {
                target.SetPageScale(page, scale);
            }
            if (markups.Count == 0)
            {
                continue;
            }
            target.SetMarkups(page, [.. target.GetMarkups(page), .. markups], outliner);
            count += markups.Count;
        }
        if (target.IsModified)
        {
            target.Save();
        }
        return count;
    }

    /// <summary>Kopiert eine Datei nach „Pläne/&lt;Plannummer&gt;/“, bei gleichem Namen mit Zählerzusatz.</summary>
    private string CopyIn(string source, string number)
    {
        var folder = Path.Combine(PlanFolder, SafeName(number));
        Directory.CreateDirectory(Path.Combine(Root, folder));
        var name = Path.GetFileNameWithoutExtension(source);
        var ext = Path.GetExtension(source);
        var relative = Path.Combine(folder, name + ext);
        for (var i = 2; File.Exists(Path.Combine(Root, relative)); i++)
        {
            relative = Path.Combine(folder, $"{name} ({i}){ext}");
        }
        File.Copy(source, Path.Combine(Root, relative));
        return relative.Replace(Path.DirectorySeparatorChar, '/');
    }

    /// <summary>Ordnername aus einer Plannummer: verbotene Zeichen werden zu „_“.</summary>
    public static string SafeName(string s)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|']).ToHashSet();
        var safe = new string(s.Trim().Select(c => invalid.Contains(c) || char.IsControl(c) ? '_' : c).ToArray()).Trim('.', ' ');
        return safe.Length == 0 ? "Ohne Nummer" : safe;
    }
}
