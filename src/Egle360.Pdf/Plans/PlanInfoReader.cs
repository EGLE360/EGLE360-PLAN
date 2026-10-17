using System.Text.RegularExpressions;

namespace Egle360.Pdf.Plans;

/// <summary>Erkannte Angaben zu einem Plan. Leere Felder wurden nicht gefunden.</summary>
public sealed record PlanInfo(string Number, string Index, string Title);

/// <summary>
/// Erkennt Plannummer, Index und Bezeichnung aus dem Text des Plankopfs und aus dem Dateinamen. Die Erkennung arbeitet
/// mit Mustern und kann danebenliegen; die Werte sind Vorschläge, die in der Planliste geändert werden können.
/// </summary>
public static partial class PlanInfoReader
{
    [GeneratedRegex(@"\bPlan\s*(?:-\s*)?(?:nummer|nr\.?|no\.?)\s*[:.]?\s*([A-Z0-9][A-Z0-9._\-/]{0,30}[A-Z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex TextNumber();

    [GeneratedRegex(@"\b(?:Index|Idx)\s*[:.]?\s*([A-Z0-9]{1,3})\b", RegexOptions.IgnoreCase)]
    private static partial Regex TextIndex();

    [GeneratedRegex(@"(?:^|[_\-\s.])(?:index|idx|rev)[_\-\s.]?([A-Z0-9]{1,3})(?=$|[_\-\s.])", RegexOptions.IgnoreCase)]
    private static partial Regex FileIndex();

    [GeneratedRegex(@"^((?:[A-Z]{1,4}[-_.]?){0,3}\d{1,6}(?:[-_.]\d{1,4})*)(?=$|[_\-\s.])", RegexOptions.IgnoreCase)]
    private static partial Regex FileNumber();

    /// <summary>Liest die Angaben aus dem Seitentext (Vorrang) und dem Dateinamen.</summary>
    public static PlanInfo Read(string fileName, string pageText)
    {
        var fromFile = FromFileName(fileName);
        var number = TextNumber().Match(pageText) is { Success: true } n && n.Groups[1].Value.Any(char.IsDigit) ? n.Groups[1].Value : fromFile.Number;
        var index = TextIndex().Match(pageText) is { Success: true } i ? i.Groups[1].Value.ToUpperInvariant() : fromFile.Index;
        return new PlanInfo(number, index, fromFile.Title);
    }

    /// <summary>Nur aus dem Dateinamen, z. B. „AR-101_Grundriss EG_Index B.pdf“.</summary>
    public static PlanInfo FromFileName(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        var index = "";
        if (FileIndex().Match(name) is { Success: true } i)
        {
            index = i.Groups[1].Value.ToUpperInvariant();
            name = name.Remove(i.Index, i.Length);
        }
        var number = "";
        if (FileNumber().Match(name) is { Success: true } n && n.Groups[1].Value.Any(char.IsDigit))
        {
            number = n.Groups[1].Value;
            name = name[n.Length..];
        }
        var title = Regex.Replace(name.Replace('_', ' '), @"\s+", " ").Trim(' ', '-', '.');
        return new PlanInfo(number, index, title);
    }
}
