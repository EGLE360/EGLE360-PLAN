using System.Globalization;
using System.Text;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Batch;

/// <summary>Ergebnis je Datei.</summary>
public enum BatchStatus
{
    Done,
    Unchanged,
    Skipped,
    Failed,
    Cancelled,
}

/// <param name="Source">Bearbeitete Datei.</param>
/// <param name="Target">Gespeicherte Datei; null, wenn nichts gespeichert wurde.</param>
public sealed record BatchEntry(string Source, string? Target, BatchStatus Status, string Message);

/// <summary>Fortschritt: <paramref name="Done"/> von <paramref name="Total"/> Dateien fertig, gerade in Arbeit <paramref name="Current"/>.</summary>
public sealed record BatchProgress(int Done, int Total, string Current, BatchEntry? Last);

/// <summary>Arbeitsschritt, der auf jede Datei angewendet wird.</summary>
public interface IBatchAction
{
    /// <summary>Kurzbeschreibung für Protokoll und Oberfläche, z. B. „Stempel GEPRÜFT auf Seite 1“.</summary>
    string Description { get; }

    /// <summary>
    /// Wendet den Schritt an. Liefert eine Meldung für das Protokoll oder null, wenn es in dieser Datei nichts zu tun
    /// gab (sie wird dann nicht gespeichert).
    /// </summary>
    string? Apply(PdfDocument document);
}

/// <summary>Wohin die Ergebnisse geschrieben werden.</summary>
/// <param name="OutputFolder">Zielordner für bearbeitete Kopien; null überschreibt die Originale.</param>
/// <param name="SkipDone">
/// Dateien überspringen, deren Kopie im Zielordner schon existiert und neuer als das Original ist. So lässt sich ein
/// abgebrochener Lauf fortsetzen.
/// </param>
public sealed record BatchOptions(string? OutputFolder, bool SkipDone = true);

/// <summary>Bericht eines Laufs.</summary>
public sealed record BatchReport(string Action, DateTimeOffset Started, DateTimeOffset Finished, IReadOnlyList<BatchEntry> Entries)
{
    public int Count(BatchStatus status) => Entries.Count(e => e.Status == status);

    /// <summary>Zusammenfassung, z. B. „12 bearbeitet, 1 übersprungen, 2 Fehler“.</summary>
    public string Summary
    {
        get
        {
            var parts = new List<string> { $"{Count(BatchStatus.Done)} bearbeitet" };
            void Add(BatchStatus s, string text)
            {
                if (Count(s) > 0)
                {
                    parts.Add($"{Count(s)} {text}");
                }
            }
            Add(BatchStatus.Unchanged, "ohne Änderung");
            Add(BatchStatus.Skipped, "übersprungen");
            Add(BatchStatus.Failed, "Fehler");
            Add(BatchStatus.Cancelled, "abgebrochen");
            return string.Join(", ", parts);
        }
    }

    /// <summary>Protokoll als Text mit einer Zeile je Datei.</summary>
    public string ToText()
    {
        var de = CultureInfo.GetCultureInfo("de-DE");
        var sb = new StringBuilder();
        sb.AppendLine("EGLE360 PLAN Stapelverarbeitung");
        sb.AppendLine($"Schritt: {Action}");
        sb.AppendLine($"Beginn: {Started.LocalDateTime.ToString("g", de)}, Ende: {Finished.LocalDateTime.ToString("g", de)}");
        sb.AppendLine($"Ergebnis: {Summary}");
        sb.AppendLine();
        foreach (var e in Entries)
        {
            sb.AppendLine($"{StatusText(e.Status)}\t{e.Source}\t{e.Message}{(e.Target is { } t && t != e.Source ? $"\t{t}" : "")}");
        }
        return sb.ToString();
    }

    public static string StatusText(BatchStatus status) => status switch
    {
        BatchStatus.Done => "Bearbeitet",
        BatchStatus.Unchanged => "Ohne Änderung",
        BatchStatus.Skipped => "Übersprungen",
        BatchStatus.Failed => "Fehler",
        _ => "Abgebrochen",
    };
}

/// <summary>
/// Wendet einen Arbeitsschritt nacheinander auf mehrere PDF-Dateien an, mit Fortschritt, Protokoll und Abbruch. Fehler
/// in einer Datei halten den Lauf nicht an.
/// </summary>
public static class BatchRunner
{
    /// <param name="between">Wird nach jeder Datei abgewartet, damit die Oberfläche sich aktualisieren kann.</param>
    public static async Task<BatchReport> RunAsync(IReadOnlyList<string> files, IBatchAction action, BatchOptions options,
        IProgress<BatchProgress>? progress, CancellationToken cancel, Func<Task>? between = null)
    {
        var started = DateTimeOffset.Now;
        var entries = new List<BatchEntry>();
        if (options.OutputFolder is { } folder)
        {
            Directory.CreateDirectory(folder);
        }
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < files.Count; i++)
        {
            var source = Path.GetFullPath(files[i]);
            if (cancel.IsCancellationRequested)
            {
                entries.AddRange(files.Skip(i).Select(f => new BatchEntry(Path.GetFullPath(f), null, BatchStatus.Cancelled, "Lauf abgebrochen.")));
                break;
            }
            progress?.Report(new BatchProgress(i, files.Count, source, entries.LastOrDefault()));
            var entry = Process(source, action, options, targets);
            entries.Add(entry);
            if (between is not null)
            {
                await between();
            }
        }
        progress?.Report(new BatchProgress(files.Count, files.Count, "", entries.LastOrDefault()));
        return new BatchReport(action.Description, started, DateTimeOffset.Now, entries);
    }

    private static BatchEntry Process(string source, IBatchAction action, BatchOptions options, HashSet<string> targets)
    {
        var target = options.OutputFolder is { } folder ? Path.Combine(Path.GetFullPath(folder), Path.GetFileName(source)) : source;
        if (!File.Exists(source))
        {
            return new BatchEntry(source, null, BatchStatus.Failed, "Datei nicht gefunden.");
        }
        if (!targets.Add(target))
        {
            return new BatchEntry(source, null, BatchStatus.Failed, "Eine andere Datei mit demselben Namen wurde bereits in den Zielordner geschrieben.");
        }
        if (options.OutputFolder is not null && string.Equals(target, source, StringComparison.OrdinalIgnoreCase))
        {
            return new BatchEntry(source, null, BatchStatus.Failed, "Zielordner und Quellordner sind gleich; bitte einen anderen Zielordner wählen.");
        }
        if (options.SkipDone && target != source && File.Exists(target) && File.GetLastWriteTimeUtc(target) >= File.GetLastWriteTimeUtc(source))
        {
            return new BatchEntry(source, target, BatchStatus.Skipped, "Im Zielordner bereits vorhanden und aktuell.");
        }
        try
        {
            using var doc = PdfDocument.Open(source);
            var message = action.Apply(doc);
            if (message is null)
            {
                return new BatchEntry(source, null, BatchStatus.Unchanged, "Nichts zu tun.");
            }
            if (target == source)
            {
                doc.Save();
            }
            else
            {
                doc.SaveAs(target);
            }
            return new BatchEntry(source, target, BatchStatus.Done, message);
        }
        catch (PdfException ex)
        {
            return new BatchEntry(source, null, BatchStatus.Failed, ex.Message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BatchEntry(source, null, BatchStatus.Failed, ex.Message);
        }
    }
}

/// <summary>Welche Seiten ein Arbeitsschritt betrifft.</summary>
public enum BatchPages
{
    First,
    All,
}

/// <summary>Ecke der Seite, wie sie auf dem Bildschirm erscheint.</summary>
public enum PageCorner
{
    TopRight,
    TopLeft,
    BottomRight,
    BottomLeft,
}

/// <summary>Setzt einen Stempel in eine Ecke der ersten oder aller Seiten.</summary>
public sealed class StampBatchAction(StampPreset stamp, string author, BatchPages pages, PageCorner corner, ITextOutliner outliner) : IBatchAction
{
    /// <summary>Abstand zum Seitenrand in Punkt.</summary>
    public const double Margin = 24;

    public string Description => $"Stempel {stamp.Title} {(pages == BatchPages.First ? "auf Seite 1" : "auf allen Seiten")}, {CornerText(corner)}";

    public static string CornerText(PageCorner corner) => corner switch
    {
        PageCorner.TopLeft => "oben links",
        PageCorner.BottomRight => "unten rechts",
        PageCorner.BottomLeft => "unten links",
        _ => "oben rechts",
    };

    public string? Apply(PdfDocument document)
    {
        if (!document.CanAnnotate)
        {
            throw new PdfException(PdfErrorKind.Permission, "Markierungen sind laut Berechtigungen des Dokuments nicht erlaubt.");
        }
        var count = pages == BatchPages.First ? Math.Min(1, document.PageCount) : document.PageCount;
        for (var p = 0; p < count; p++)
        {
            var m = Place(document.GetPageTransform(p));
            document.SetMarkups(p, [.. document.GetMarkups(p), m], outliner);
        }
        return count == 0 ? null : count == 1 ? "Stempel auf 1 Seite gesetzt." : $"Stempel auf {count} Seiten gesetzt.";
    }

    /// <summary>Stempel in Standardgröße in der gewählten Ecke, aufrecht wie die Seite angezeigt wird.</summary>
    public Markup Place(Viewing.PageTransform t)
    {
        var (w, h) = StampPreset.DefaultSize;
        var u = corner is PageCorner.TopLeft or PageCorner.BottomLeft ? Margin : t.DisplayWidth - Margin - w;
        var v = corner is PageCorner.TopLeft or PageCorner.TopRight ? Margin : t.DisplayHeight - Margin - h;
        var box = PdfRect.FromPoints(t.ToUser(u, v), t.ToUser(u + w, v + h));
        var style = MarkupStyle.DefaultFor(MarkupKind.Stamp) with { Stroke = stamp.Color };
        return Markup.Create(MarkupKind.Stamp, [new[] { new PdfPoint(box.Left, box.Bottom), new PdfPoint(box.Right, box.Top) }], style, author, t.Rotation, stamp.Title);
    }
}

/// <summary>Legt den Maßstab der ersten oder aller Seiten fest.</summary>
public sealed class ScaleBatchAction(MeasureScale scale, BatchPages pages) : IBatchAction
{
    public string Description => $"Maßstab {scale.Label} {(pages == BatchPages.First ? "für Seite 1" : "für alle Seiten")}";

    public string? Apply(PdfDocument document)
    {
        var count = pages == BatchPages.First ? Math.Min(1, document.PageCount) : document.PageCount;
        var changed = 0;
        for (var p = 0; p < count; p++)
        {
            if (document.GetPageScale(p) is { } current && current.MetersPerPoint == scale.MetersPerPoint && current.Unit == scale.Unit)
            {
                continue;
            }
            document.SetPageScale(p, scale);
            changed++;
        }
        return changed == 0 ? null : changed == 1 ? $"Maßstab {scale.Label} für 1 Seite gesetzt." : $"Maßstab {scale.Label} für {changed} Seiten gesetzt.";
    }
}
