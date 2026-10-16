using Egle360.Pdf.Markups;
using Egle360.Pdf.Native;

namespace Egle360.Pdf;

/// <summary>Fundstelle einer Textsuche: Seite, Position im Seitentext und Rechtecke in Benutzerkoordinaten.</summary>
public sealed record PdfTextMatch(int Page, int Start, int Length, IReadOnlyList<PdfRect> Rects, string Context);

/// <summary>Seitentext lesen und durchsuchen.</summary>
public sealed unsafe partial class PdfDocument
{
    /// <summary>Gesamter Text einer Seite in Lesereihenfolge, wie PDFium ihn erkennt. Gescannte Pläne liefern einen leeren Text.</summary>
    public string GetPageText(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            var text = Pdfium.FPDFText_LoadPage(GetPage(pageIndex));
            if (text == 0)
            {
                return "";
            }
            try
            {
                return ReadText(text);
            }
            finally
            {
                Pdfium.FPDFText_ClosePage(text);
            }
        }
    }

    /// <summary>
    /// Zeilenrechtecke des Textes, dessen Zeichen mit der Mitte im Bereich <paramref name="area"/> liegen, in
    /// Lesereihenfolge. Grundlage für Hervorheben, Unterstreichen und Durchstreichen von Text. Leer, wenn dort kein Text ist.
    /// </summary>
    public IReadOnlyList<PdfRect> GetTextRects(int pageIndex, PdfRect area)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            var text = Pdfium.FPDFText_LoadPage(GetPage(pageIndex));
            if (text == 0)
            {
                return [];
            }
            try
            {
                var count = Pdfium.FPDFText_CountChars(text);
                var result = new List<PdfRect>();
                var runStart = -1;
                for (var i = 0; i <= count; i++)
                {
                    var inside = false;
                    if (i < count)
                    {
                        double l, r, b, t;
                        if (Pdfium.FPDFText_GetCharBox(text, i, &l, &r, &b, &t) != 0 && r > l && t > b)
                        {
                            inside = area.Contains(new PdfPoint((l + r) / 2, (b + t) / 2));
                        }
                    }
                    if (inside && runStart < 0)
                    {
                        runStart = i;
                    }
                    else if (!inside && runStart >= 0)
                    {
                        AddRects(text, runStart, i - runStart, result);
                        runStart = -1;
                    }
                }
                return result;
            }
            finally
            {
                Pdfium.FPDFText_ClosePage(text);
            }
        }
    }

    private static void AddRects(nint text, int start, int count, List<PdfRect> result)
    {
        var n = Pdfium.FPDFText_CountRects(text, start, count);
        for (var k = 0; k < n; k++)
        {
            double l, t, r, b;
            if (Pdfium.FPDFText_GetRect(text, k, &l, &t, &r, &b) != 0 && r - l > 0.1 && Math.Abs(t - b) > 0.1)
            {
                result.Add(new PdfRect(Math.Min(l, r), Math.Min(b, t), Math.Max(l, r), Math.Max(b, t)));
            }
        }
    }

    /// <summary>
    /// Sucht den Begriff auf einer Seite, ohne Groß- und Kleinschreibung zu unterscheiden. Leerräume aller Art (auch
    /// Zeilenumbrüche) gelten als gleich, damit Begriffe über Zeilenenden hinweg gefunden werden.
    /// </summary>
    public IReadOnlyList<PdfTextMatch> FindText(int pageIndex, string query)
    {
        var needle = Normalize(query.Trim());
        if (needle.Length == 0)
        {
            return [];
        }
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            var text = Pdfium.FPDFText_LoadPage(GetPage(pageIndex));
            if (text == 0)
            {
                return [];
            }
            try
            {
                var raw = ReadText(text);
                var hay = Normalize(raw);
                var result = new List<PdfTextMatch>();
                for (var at = hay.IndexOf(needle, StringComparison.Ordinal); at >= 0; at = hay.IndexOf(needle, at + needle.Length, StringComparison.Ordinal))
                {
                    var rects = new List<PdfRect>();
                    var count = Pdfium.FPDFText_CountRects(text, at, needle.Length);
                    for (var i = 0; i < count; i++)
                    {
                        double l, t, r, b;
                        if (Pdfium.FPDFText_GetRect(text, i, &l, &t, &r, &b) != 0)
                        {
                            rects.Add(new PdfRect(Math.Min(l, r), Math.Min(b, t), Math.Max(l, r), Math.Max(b, t)));
                        }
                    }
                    result.Add(new PdfTextMatch(pageIndex, at, needle.Length, rects, Context(raw, at, needle.Length)));
                }
                return result;
            }
            finally
            {
                Pdfium.FPDFText_ClosePage(text);
            }
        }
    }

    private static string ReadText(nint text)
    {
        var count = Pdfium.FPDFText_CountChars(text);
        if (count <= 0)
        {
            return "";
        }
        var buffer = new char[count + 1];
        fixed (char* p = buffer)
        {
            var written = Pdfium.FPDFText_GetText(text, 0, count, p);
            return new string(buffer, 0, Math.Clamp(written - 1, 0, count));
        }
    }

    /// <summary>Kleinbuchstaben und einheitliche Leerzeichen, Zeichen für Zeichen, damit Positionen erhalten bleiben.</summary>
    internal static string Normalize(string s) =>
        string.Create(s.Length, s, (span, src) =>
        {
            for (var i = 0; i < src.Length; i++)
            {
                var c = src[i];
                span[i] = char.IsWhiteSpace(c) || c == '\0' ? ' ' : char.ToLowerInvariant(c);
            }
        });

    private static string Context(string raw, int start, int length)
    {
        const int Around = 30;
        var from = Math.Max(0, start - Around);
        var to = Math.Min(raw.Length, start + length + Around);
        var snippet = raw[from..to].ReplaceLineEndings(" ").Replace('\0', ' ');
        return (from > 0 ? "…" : "") + snippet.Trim() + (to < raw.Length ? "…" : "");
    }
}
