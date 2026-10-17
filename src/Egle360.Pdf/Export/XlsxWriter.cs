using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Text;

namespace Egle360.Pdf.Export;

/// <summary>Zellformat. Die Reihenfolge entspricht den Einträgen in cellXfs von styles.xml.</summary>
public enum XlsxStyle
{
    Default = 0,
    Header = 1,
    Number = 2,
    Date = 3,
    Integer = 4,
}

/// <summary>Eine Zelle: Text oder Zahl (Datumswerte als Excel-Seriennummer).</summary>
public readonly record struct XlsxCell(string? Content, double? Value, XlsxStyle Style)
{
    public static XlsxCell Header(string text) => new(text, null, XlsxStyle.Header);
    public static XlsxCell Text(string text) => new(text, null, XlsxStyle.Default);
    public static XlsxCell Number(double? value) => new(null, value, XlsxStyle.Number);
    public static XlsxCell Integer(int value) => new(null, value, XlsxStyle.Integer);
    public static XlsxCell Date(DateTime value) => new(null, value.ToOADate(), XlsxStyle.Date);
}

/// <summary>Ein Tabellenblatt; die erste Zeile wird fixiert, wenn sie eine Überschrift ist.</summary>
public sealed class XlsxSheet(string name, IReadOnlyList<double> columnWidths)
{
    public string Name { get; } = name;
    public IReadOnlyList<double> ColumnWidths { get; } = columnWidths;
    public List<XlsxCell[]> Rows { get; } = [];
}

/// <summary>
/// Schreibt eine schlichte Excel-Datei (Office Open XML, SpreadsheetML) ohne Fremdbibliothek: Texte als Inline-Strings,
/// Zahlen mit zwei Nachkommastellen, Datumswerte, fette Überschrift mit Filter.
/// </summary>
public static class XlsxWriter
{
    private const string Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    public static byte[] Write(IReadOnlyList<XlsxSheet> sheets)
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            void Add(string path, string content)
            {
                var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
                using var w = new StreamWriter(entry.Open(), new UTF8Encoding(false));
                w.Write(content);
            }

            var types = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
                .Append("<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">")
                .Append("<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>")
                .Append("<Default Extension=\"xml\" ContentType=\"application/xml\"/>")
                .Append("<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>")
                .Append("<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>");
            for (var i = 1; i <= sheets.Count; i++)
            {
                types.Append($"<Override PartName=\"/xl/worksheets/sheet{i}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>");
            }
            Add("[Content_Types].xml", types.Append("</Types>").ToString());

            Add("_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
                + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
                + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
                + "</Relationships>");

            var workbook = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"{Ns}\" xmlns:r=\"{RelNs}\"><sheets>");
            var rels = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">");
            for (var i = 1; i <= sheets.Count; i++)
            {
                workbook.Append($"<sheet name=\"{Escape(SheetName(sheets[i - 1].Name))}\" sheetId=\"{i}\" r:id=\"rId{i}\"/>");
                rels.Append($"<Relationship Id=\"rId{i}\" Type=\"{RelNs}/worksheet\" Target=\"worksheets/sheet{i}.xml\"/>");
            }
            rels.Append($"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"{RelNs}/styles\" Target=\"styles.xml\"/>");
            Add("xl/workbook.xml", workbook.Append("</sheets></workbook>").ToString());
            Add("xl/_rels/workbook.xml.rels", rels.Append("</Relationships>").ToString());
            Add("xl/styles.xml", Styles);
            for (var i = 0; i < sheets.Count; i++)
            {
                Add($"xl/worksheets/sheet{i + 1}.xml", Sheet(sheets[i]));
            }
        }
        return stream.ToArray();
    }

    // numFmtId 4 = #,##0.00, 1 = 0; 164 ist ein eigenes Datumsformat. Excel zeigt die Trennzeichen je nach Ländereinstellung.
    private const string Styles = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
        + $"<styleSheet xmlns=\"{Ns}\">"
        + "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd/mm/yyyy\\ hh:mm\"/></numFmts>"
        + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
        + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
        + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
        + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
        + "<cellXfs count=\"5\">"
        + "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
        + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>"
        + "<xf numFmtId=\"4\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "<xf numFmtId=\"1\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/>"
        + "</cellXfs>"
        + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
        + "</styleSheet>";

    private static string Sheet(XlsxSheet sheet)
    {
        var headerRow = sheet.Rows.Count > 1 && sheet.Rows[0].Length > 1 && sheet.Rows[0].All(c => c.Style == XlsxStyle.Header);
        var sb = new StringBuilder($"<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"{Ns}\">");
        if (headerRow)
        {
            sb.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
        }
        if (sheet.ColumnWidths.Count > 0)
        {
            sb.Append("<cols>");
            for (var c = 0; c < sheet.ColumnWidths.Count; c++)
            {
                sb.Append(CultureInfo.InvariantCulture, $"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{sheet.ColumnWidths[c]}\" customWidth=\"1\"/>");
            }
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (var r = 0; r < sheet.Rows.Count; r++)
        {
            sb.Append($"<row r=\"{r + 1}\">");
            var row = sheet.Rows[r];
            for (var c = 0; c < row.Length; c++)
            {
                var cell = row[c];
                var reference = ColumnName(c) + (r + 1);
                var style = cell.Style == XlsxStyle.Default ? "" : $" s=\"{(int)cell.Style}\"";
                if (cell.Value is { } v && double.IsFinite(v))
                {
                    sb.Append(CultureInfo.InvariantCulture, $"<c r=\"{reference}\"{style}><v>{v:R}</v></c>");
                }
                else if (!string.IsNullOrEmpty(cell.Content))
                {
                    sb.Append($"<c r=\"{reference}\"{style} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Escape(cell.Content)}</t></is></c>");
                }
            }
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        if (headerRow)
        {
            sb.Append($"<autoFilter ref=\"A1:{ColumnName(sheet.Rows[0].Length - 1)}{sheet.Rows.Count}\"/>");
        }
        return sb.Append("</worksheet>").ToString();
    }

    public static string ColumnName(int index)
    {
        var name = "";
        for (var n = index + 1; n > 0; n = (n - 1) / 26)
        {
            name = (char)('A' + (n - 1) % 26) + name;
        }
        return name;
    }

    /// <summary>Excel erlaubt höchstens 31 Zeichen und keine der Zeichen : \ / ? * [ ] im Blattnamen.</summary>
    private static string SheetName(string name)
    {
        var clean = new string([.. name.Select(ch => ":\\/?*[]".Contains(ch) ? ' ' : ch)]).Trim();
        return clean.Length == 0 ? "Blatt" : clean[..Math.Min(31, clean.Length)];
    }

    /// <summary>XML-Escape; Steuerzeichen außer Tab und Zeilenumbruch sind in XML 1.0 nicht erlaubt und entfallen.</summary>
    private static string Escape(string s) =>
        SecurityElement.Escape(new string([.. s.Where(ch => ch is '\t' or '\n' or '\r' || ch >= ' ')])) ?? "";
}
