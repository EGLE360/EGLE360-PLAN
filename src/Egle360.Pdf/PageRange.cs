using System.Text.RegularExpressions;

namespace Egle360.Pdf;

/// <summary>Seitenbereiche wie „1-3, 5“ (1-basiert) prüfen und in die Form „1-3,5“ bringen.</summary>
public static partial class PageRange
{
    [GeneratedRegex(@"^\s*\d+\s*(-\s*\d+\s*)?(,\s*\d+\s*(-\s*\d+\s*)?)*$")]
    private static partial Regex Syntax();

    /// <summary>Liefert den bereinigten Bereich oder wirft eine <see cref="FormatException"/> mit verständlicher Meldung.</summary>
    public static string Normalize(string text, int pageCount)
    {
        if (string.IsNullOrWhiteSpace(text) || !Syntax().IsMatch(text))
        {
            throw new FormatException("Bitte Seiten wie „1-3, 5“ angeben.");
        }
        var parts = new List<string>();
        foreach (var part in text.Split(',', StringSplitOptions.TrimEntries))
        {
            var bounds = part.Split('-', StringSplitOptions.TrimEntries).Select(int.Parse).ToArray();
            var from = bounds[0];
            var to = bounds.Length > 1 ? bounds[1] : from;
            if (from < 1 || to > pageCount || from > to)
            {
                throw new FormatException($"Der Bereich „{part}“ passt nicht zu den Seiten 1 bis {pageCount}.");
            }
            parts.Add(from == to ? $"{from}" : $"{from}-{to}");
        }
        return string.Join(",", parts);
    }

    /// <summary>Anzahl der Seiten in einem bereinigten Bereich (Doppelte zählen mehrfach, wie beim Einfügen).</summary>
    public static int Count(string normalized) =>
        normalized.Split(',').Sum(p => p.Split('-') is [var a, var b] ? int.Parse(b) - int.Parse(a) + 1 : 1);
}
