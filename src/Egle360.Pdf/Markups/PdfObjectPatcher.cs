using System.Text;

namespace Egle360.Pdf.Markups;

/// <summary>
/// Überträgt nach dem Speichern die Einträge aus <see cref="MarkupSerializer.StandardKey"/> in die Anmerkung selbst.
/// </summary>
/// <remarks>
/// PDFium kann Linien-, Polylinien- und Polygonanmerkungen weder anlegen noch deren Geometrieschlüssel (L, Vertices,
/// LE, BE) setzen. EGLE360 legt sie deshalb als Ink-Anmerkung an und hinterlegt die Standardeinträge als Text. Nach
/// dem Schreiben wird jedes betroffene Anmerkungswörterbuch in der Datei neu zusammengesetzt, und zwar mit höchstens
/// gleicher Länge (Rest mit Leerzeichen aufgefüllt). Dadurch bleiben alle Byte-Positionen der Querverweistabelle
/// gültig. Kann ein Wörterbuch nicht sicher umgeschrieben werden, bleibt es unverändert: Die Datei ist dann weiterhin
/// gültig, die Markierung ist nur eine Ink-Anmerkung mit eigener Darstellung.
/// </remarks>
internal static class PdfObjectPatcher
{
    private static readonly byte[] Marker = Encoding.ASCII.GetBytes("/" + MarkupSerializer.StandardKey);
    private static readonly HashSet<string> AllowedPayloadKeys = ["Subtype", "L", "LE", "Vertices", "BE", "IT", "Name", "Measure"];
    private const int MaxObjectSize = 64 * 1024 * 1024;

    /// <summary>Durchsucht den Datenstrom ab <paramref name="from"/> und schreibt alle betroffenen Objekte um.</summary>
    /// <returns>Anzahl der umgeschriebenen Anmerkungen.</returns>
    public static int Patch(Stream stream, long from)
    {
        if (!stream.CanRead || !stream.CanSeek || !stream.CanWrite)
        {
            return 0;
        }
        var patched = 0;
        var doneUntil = -1L;
        foreach (var hit in FindMarkers(stream, from))
        {
            if (hit < doneUntil)
            {
                continue;
            }
            if (TryLocateObject(stream, hit, out var start, out var bytes))
            {
                var count = PatchObject(bytes);
                if (count > 0)
                {
                    stream.Position = start;
                    stream.Write(bytes);
                    patched += count;
                }
                doneUntil = start + bytes.Length;
            }
        }
        stream.Seek(0, SeekOrigin.End);
        return patched;
    }

    /// <summary>Schreibt alle Wörterbücher mit dem Hilfsschlüssel in einem einzelnen Objekt um (gleiche Länge).</summary>
    internal static int PatchObject(byte[] obj)
    {
        Node root;
        try
        {
            var pos = 0;
            root = new Parser(obj).ParseValue(ref pos);
        }
        catch (FormatException)
        {
            return 0;
        }
        var targets = new List<Node>();
        Collect(root, targets);
        var count = 0;
        foreach (var dict in targets.OrderByDescending(t => t.Start))
        {
            var replacement = Rebuild(obj, dict);
            if (replacement is null || replacement.Length > dict.End - dict.Start)
            {
                continue;
            }
            var span = obj.AsSpan(dict.Start, dict.End - dict.Start);
            span.Fill((byte)' ');
            replacement.AsSpan(0, replacement.Length - 2).CopyTo(span);
            span[^2] = (byte)'>';
            span[^1] = (byte)'>';
            count++;
        }
        return count;
    }

    private static void Collect(Node node, List<Node> targets)
    {
        if (node.Kind == NodeKind.Dict && node.Entries.Any(e => e.Key == MarkupSerializer.StandardKey))
        {
            targets.Add(node);
            return;
        }
        foreach (var child in node.Entries.Select(e => e.Value).Concat(node.Items))
        {
            Collect(child, targets);
        }
    }

    private static byte[]? Rebuild(byte[] source, Node dict)
    {
        var std = dict.Entries.First(e => e.Key == MarkupSerializer.StandardKey).Value;
        if (std.Kind != NodeKind.LiteralString)
        {
            return null;
        }
        var payload = DecodeLiteral(source.AsSpan(std.Start, std.End - std.Start));
        Node extra;
        try
        {
            var pos = 0;
            extra = new Parser(payload).ParseValue(ref pos);
        }
        catch (FormatException)
        {
            return null;
        }
        if (extra.Kind != NodeKind.Dict || extra.Entries.Any(e => !AllowedPayloadKeys.Contains(e.Key)))
        {
            return null;
        }
        var replaced = extra.Entries.Select(e => e.Key).ToHashSet();
        var dropInk = replaced.Contains("Subtype");

        var sb = new MemoryStream();
        sb.Write("<<"u8);
        foreach (var e in dict.Entries)
        {
            if (e.Key == MarkupSerializer.StandardKey || replaced.Contains(e.Key) || (dropInk && e.Key == "InkList"))
            {
                continue;
            }
            WriteEntry(sb, e.Key, source.AsSpan(e.Value.Start, e.Value.End - e.Value.Start));
        }
        foreach (var e in extra.Entries)
        {
            WriteEntry(sb, e.Key, payload.AsSpan(e.Value.Start, e.Value.End - e.Value.Start));
        }
        sb.Write(">>"u8);
        return sb.ToArray();
    }

    private static void WriteEntry(Stream s, string key, ReadOnlySpan<byte> value)
    {
        if (s.Length > 2)
        {
            s.WriteByte((byte)' ');
        }
        s.WriteByte((byte)'/');
        s.Write(Encoding.ASCII.GetBytes(key));
        s.WriteByte((byte)' ');
        s.Write(value);
    }

    // ---------- Suche in der Datei ----------

    private static IEnumerable<long> FindMarkers(Stream stream, long from)
    {
        const int chunk = 4 * 1024 * 1024;
        var buffer = new byte[chunk + Marker.Length];
        var position = from;
        var length = stream.Length;
        while (position < length)
        {
            stream.Position = position;
            var read = ReadFully(stream, buffer, (int)Math.Min(buffer.Length, length - position));
            var span = buffer.AsSpan(0, read);
            var offset = 0;
            var hits = new List<long>();
            while (true)
            {
                var i = span[offset..].IndexOf(Marker);
                if (i < 0)
                {
                    break;
                }
                hits.Add(position + offset + i);
                offset += i + Marker.Length;
            }
            foreach (var h in hits)
            {
                yield return h;
            }
            if (read < buffer.Length)
            {
                break;
            }
            position += chunk;
        }
    }

    /// <summary>Sucht rückwärts den Beginn des Objekts („N G obj“) und liest es bis „endobj“.</summary>
    private static bool TryLocateObject(Stream stream, long hit, out long start, out byte[] body)
    {
        start = 0;
        body = [];
        // Zuerst ein kleines Fenster, bei Bedarf schrittweise größer (große Seitenobjekte mit vielen Anmerkungen).
        for (var size = 256 * 1024; size <= MaxObjectSize; size *= 4)
        {
            var windowStart = Math.Max(0, hit - size);
            var window = new byte[(int)(hit - windowStart)];
            stream.Position = windowStart;
            ReadFully(stream, window, window.Length);
            var i = window.AsSpan().LastIndexOf("obj"u8);
            while (i >= 0 && !IsObjHeader(window, i))
            {
                i = window.AsSpan(0, i).LastIndexOf("obj"u8);
            }
            if (i < 0)
            {
                if (windowStart == 0)
                {
                    return false;
                }
                continue;
            }
            return TryReadObject(stream, windowStart + i + 3, hit, out start, out body);
        }
        return false;
    }

    private static bool TryReadObject(Stream stream, long valueStart, long hit, out long start, out byte[] body)
    {
        start = 0;
        body = [];
        for (var size = 256 * 1024; ; size *= 4)
        {
            var length = (int)Math.Min(Math.Min(size, MaxObjectSize), stream.Length - valueStart);
            var candidate = new byte[length];
            stream.Position = valueStart;
            var read = ReadFully(stream, candidate, length);
            try
            {
                var parser = new Parser(candidate);
                var pos = 0;
                parser.SkipWhitespace(ref pos);
                var first = pos;
                var node = parser.ParseValue(ref pos);
                parser.SkipWhitespace(ref pos);
                var endsWithEndobj = candidate.AsSpan(pos, Math.Min(6, read - pos)).SequenceEqual("endobj"u8);
                if (!endsWithEndobj || valueStart + node.End <= hit || node.Kind != NodeKind.Dict)
                {
                    return false;
                }
                start = valueStart + first;
                body = candidate.AsSpan(first, node.End - first).ToArray();
                return true;
            }
            catch (FormatException) when (length == size && size < MaxObjectSize)
            {
                // Vermutlich abgeschnitten; mit größerem Fenster erneut versuchen.
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }

    private static bool IsObjHeader(byte[] w, int i)
    {
        // Davor: Leerraum, Generationsnummer, Leerraum, Objektnummer; danach Leerraum oder Begrenzer.
        if (i + 3 < w.Length && !Parser.IsWhitespace(w[i + 3]) && !Parser.IsDelimiter(w[i + 3]))
        {
            return false;
        }
        var p = i - 1;
        if (p < 0 || !Parser.IsWhitespace(w[p]))
        {
            return false;
        }
        while (p >= 0 && Parser.IsWhitespace(w[p])) p--;
        var digits = 0;
        while (p >= 0 && char.IsAsciiDigit((char)w[p])) { p--; digits++; }
        if (digits == 0 || p < 0 || !Parser.IsWhitespace(w[p]))
        {
            return false;
        }
        while (p >= 0 && Parser.IsWhitespace(w[p])) p--;
        digits = 0;
        while (p >= 0 && char.IsAsciiDigit((char)w[p])) { p--; digits++; }
        return digits > 0 && (p < 0 || Parser.IsWhitespace(w[p]) || Parser.IsDelimiter(w[p]));
    }

    private static int ReadFully(Stream s, byte[] buffer, int count)
    {
        var total = 0;
        while (total < count)
        {
            var n = s.Read(buffer, total, count - total);
            if (n <= 0)
            {
                break;
            }
            total += n;
        }
        return total;
    }

    /// <summary>Entschlüsselt die Escape-Sequenzen eines Literal-Strings (Bereich einschließlich Klammern).</summary>
    internal static byte[] DecodeLiteral(ReadOnlySpan<byte> raw)
    {
        var result = new List<byte>(raw.Length);
        for (var i = 1; i < raw.Length - 1; i++)
        {
            var c = raw[i];
            if (c != '\\')
            {
                result.Add(c);
                continue;
            }
            i++;
            if (i >= raw.Length - 1)
            {
                break;
            }
            c = raw[i];
            switch (c)
            {
                case (byte)'n': result.Add((byte)'\n'); break;
                case (byte)'r': result.Add((byte)'\r'); break;
                case (byte)'t': result.Add((byte)'\t'); break;
                case (byte)'b': result.Add(8); break;
                case (byte)'f': result.Add(12); break;
                case (byte)'\r':
                    if (i + 1 < raw.Length - 1 && raw[i + 1] == '\n') i++;
                    break;
                case (byte)'\n': break;
                default:
                    if (c >= '0' && c <= '7')
                    {
                        var v = c - '0';
                        for (var k = 0; k < 2 && i + 1 < raw.Length - 1 && raw[i + 1] >= '0' && raw[i + 1] <= '7'; k++)
                        {
                            v = v * 8 + (raw[++i] - '0');
                        }
                        result.Add((byte)v);
                    }
                    else
                    {
                        result.Add(c);
                    }
                    break;
            }
        }
        return result.ToArray();
    }

    // ---------- Minimaler Parser für PDF-Objekte ----------

    internal enum NodeKind
    {
        Dict,
        Array,
        LiteralString,
        HexString,
        Name,
        Number,
        Reference,
        Keyword,
    }

    internal sealed class Node(NodeKind kind, int start)
    {
        public NodeKind Kind { get; } = kind;

        public int Start { get; } = start;

        public int End { get; set; }

        public List<(string Key, Node Value)> Entries { get; } = [];

        public List<Node> Items { get; } = [];
    }

    internal sealed class Parser(byte[] data)
    {
        public static bool IsWhitespace(byte c) => c is 0 or 9 or 10 or 12 or 13 or 32;

        public static bool IsDelimiter(byte c) => c is (byte)'(' or (byte)')' or (byte)'<' or (byte)'>' or (byte)'[' or (byte)']' or (byte)'{' or (byte)'}' or (byte)'/' or (byte)'%';

        public void SkipWhitespace(ref int pos)
        {
            while (pos < data.Length)
            {
                if (IsWhitespace(data[pos]))
                {
                    pos++;
                }
                else if (data[pos] == '%')
                {
                    while (pos < data.Length && data[pos] != '\n' && data[pos] != '\r') pos++;
                }
                else
                {
                    break;
                }
            }
        }

        public Node ParseValue(ref int pos)
        {
            SkipWhitespace(ref pos);
            if (pos >= data.Length)
            {
                throw new FormatException("Unerwartetes Ende.");
            }
            var c = data[pos];
            if (c == '<' && pos + 1 < data.Length && data[pos + 1] == '<')
            {
                return ParseDict(ref pos);
            }
            return c switch
            {
                (byte)'<' => ParseHex(ref pos),
                (byte)'[' => ParseArray(ref pos),
                (byte)'(' => ParseLiteral(ref pos),
                (byte)'/' => ParseName(ref pos, out _),
                _ when c is (byte)'+' or (byte)'-' or (byte)'.' || char.IsAsciiDigit((char)c) => ParseNumberOrRef(ref pos),
                _ when char.IsAsciiLetter((char)c) => ParseKeyword(ref pos),
                _ => throw new FormatException($"Unerwartetes Zeichen an Position {pos}."),
            };
        }

        private Node ParseDict(ref int pos)
        {
            var node = new Node(NodeKind.Dict, pos);
            pos += 2;
            while (true)
            {
                SkipWhitespace(ref pos);
                if (pos + 1 < data.Length && data[pos] == '>' && data[pos + 1] == '>')
                {
                    pos += 2;
                    node.End = pos;
                    return node;
                }
                if (pos >= data.Length || data[pos] != '/')
                {
                    throw new FormatException("Schlüssel erwartet.");
                }
                ParseName(ref pos, out var key);
                var value = ParseValue(ref pos);
                node.Entries.Add((key, value));
            }
        }

        private Node ParseArray(ref int pos)
        {
            var node = new Node(NodeKind.Array, pos);
            pos++;
            while (true)
            {
                SkipWhitespace(ref pos);
                if (pos >= data.Length)
                {
                    throw new FormatException("Array nicht abgeschlossen.");
                }
                if (data[pos] == ']')
                {
                    pos++;
                    node.End = pos;
                    return node;
                }
                node.Items.Add(ParseValue(ref pos));
            }
        }

        private Node ParseLiteral(ref int pos)
        {
            var node = new Node(NodeKind.LiteralString, pos);
            var depth = 0;
            for (; pos < data.Length; pos++)
            {
                var c = data[pos];
                if (c == '\\')
                {
                    pos++;
                }
                else if (c == '(')
                {
                    depth++;
                }
                else if (c == ')' && --depth == 0)
                {
                    pos++;
                    node.End = pos;
                    return node;
                }
            }
            throw new FormatException("Zeichenkette nicht abgeschlossen.");
        }

        private Node ParseHex(ref int pos)
        {
            var node = new Node(NodeKind.HexString, pos);
            var end = Array.IndexOf(data, (byte)'>', pos);
            if (end < 0)
            {
                throw new FormatException("Hex-Zeichenkette nicht abgeschlossen.");
            }
            pos = end + 1;
            node.End = pos;
            return node;
        }

        private Node ParseName(ref int pos, out string name)
        {
            var node = new Node(NodeKind.Name, pos);
            pos++;
            var start = pos;
            while (pos < data.Length && !IsWhitespace(data[pos]) && !IsDelimiter(data[pos])) pos++;
            name = Encoding.Latin1.GetString(data, start, pos - start);
            node.End = pos;
            return node;
        }

        private Node ParseNumberOrRef(ref int pos)
        {
            var node = new Node(NodeKind.Number, pos);
            var first = ReadRegular(ref pos);
            node.End = pos;
            if (IsInteger(first))
            {
                // Möglicher Verweis „N G R“.
                var p = pos;
                SkipWhitespace(ref p);
                var gen = ReadRegular(ref p);
                if (IsInteger(gen))
                {
                    SkipWhitespace(ref p);
                    if (p < data.Length && data[p] == 'R' && (p + 1 >= data.Length || IsWhitespace(data[p + 1]) || IsDelimiter(data[p + 1])))
                    {
                        pos = p + 1;
                        var reference = new Node(NodeKind.Reference, node.Start) { End = pos };
                        return reference;
                    }
                }
            }
            return node;
        }

        private Node ParseKeyword(ref int pos)
        {
            var node = new Node(NodeKind.Keyword, pos);
            ReadRegular(ref pos);
            node.End = pos;
            return node;
        }

        private string ReadRegular(ref int pos)
        {
            var start = pos;
            while (pos < data.Length && !IsWhitespace(data[pos]) && !IsDelimiter(data[pos])) pos++;
            return Encoding.Latin1.GetString(data, start, pos - start);
        }

        private static bool IsInteger(string s) => s.Length > 0 && s.All(char.IsAsciiDigit);
    }
}
