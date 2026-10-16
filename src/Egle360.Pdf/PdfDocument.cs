using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Egle360.Pdf.Native;
using Microsoft.Win32.SafeHandles;

namespace Egle360.Pdf;

/// <summary>
/// Ein geöffnetes PDF-Dokument. Alle Methoden sind threadsicher, weil sie die gemeinsame PDFium-Sperre nutzen.
/// </summary>
/// <remarks>
/// Speichern ohne Seitenstrukturänderung erfolgt inkrementell: Die Originalbytes bleiben unverändert,
/// Änderungen werden angehängt. Nach dem Löschen oder Einfügen von Seiten wird neu geschrieben, damit entfernte
/// Inhalte nicht in der Datei verbleiben.
/// </remarks>
public sealed unsafe class PdfDocument : IDisposable
{
    private const int PageCacheSize = 8;

    private nint _handle;
    private IDocumentSource? _source;
    private readonly LinkedList<(int Index, nint Page)> _pageCache = new();
    private bool _structureChanged;

    private PdfDocument(nint handle, IDocumentSource? source, string? path)
    {
        _handle = handle;
        _source = source;
        FilePath = path;
    }

    /// <summary>Pfad der Datei, aus der das Dokument geladen wurde; null bei neuen oder im Speicher geladenen Dokumenten.</summary>
    public string? FilePath { get; private set; }

    /// <summary>Kennwort, mit dem das Dokument geöffnet wurde. Wird zum Neuladen nach dem Speichern gebraucht.</summary>
    private string? Password { get; set; }

    public bool IsModified { get; private set; }

    /// <summary>Wird ausgelöst, nachdem sich Seitenanzahl, Reihenfolge oder Drehung geändert haben.</summary>
    public event EventHandler? PagesChanged;

    public static PdfDocument Open(string path, string? password = null)
    {
        PdfiumLibrary.EnsureInitialized();
        if (!File.Exists(path))
        {
            throw new PdfException(PdfErrorKind.File, $"Öffnen von {Path.GetFileName(path)}: {PdfException.Describe(PdfErrorKind.File)}");
        }
        var source = new FileSource(path);
        try
        {
            var handle = source.Load(password);
            return new PdfDocument(handle, source, Path.GetFullPath(path)) { Password = password };
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    public static PdfDocument Open(ReadOnlySpan<byte> data, string? password = null)
    {
        PdfiumLibrary.EnsureInitialized();
        var source = new MemorySource(data);
        try
        {
            var handle = source.Load(password);
            return new PdfDocument(handle, source, null) { Password = password };
        }
        catch
        {
            source.Dispose();
            throw;
        }
    }

    /// <summary>Erstellt ein leeres Dokument ohne Seiten.</summary>
    public static PdfDocument CreateNew()
    {
        PdfiumLibrary.EnsureInitialized();
        lock (PdfiumLibrary.Sync)
        {
            var handle = Pdfium.FPDF_CreateNewDocument();
            if (handle == 0)
            {
                throw PdfiumLibrary.LastError("Neues Dokument");
            }
            return new PdfDocument(handle, null, null) { IsModified = true, _structureChanged = true };
        }
    }

    public int PageCount
    {
        get
        {
            lock (PdfiumLibrary.Sync)
            {
                return Pdfium.FPDF_GetPageCount(Handle);
            }
        }
    }

    /// <summary>PDF-Version als Zahl, z. B. 17 für PDF 1.7; null bei neuen Dokumenten.</summary>
    public int? FileVersion
    {
        get
        {
            lock (PdfiumLibrary.Sync)
            {
                int v;
                return Pdfium.FPDF_GetFileVersion(Handle, &v) != 0 ? v : null;
            }
        }
    }

    public bool IsEncrypted
    {
        get
        {
            lock (PdfiumLibrary.Sync)
            {
                return Pdfium.FPDF_GetSecurityHandlerRevision(Handle) != -1;
            }
        }
    }

    public PdfPermissions Permissions
    {
        get
        {
            lock (PdfiumLibrary.Sync)
            {
                // Unverschlüsselte und neue Dokumente haben keine Einschränkungen; PDFium meldet dort teils 0.
                if (Pdfium.FPDF_GetSecurityHandlerRevision(Handle) == -1)
                {
                    return (PdfPermissions)uint.MaxValue;
                }
                return (PdfPermissions)(uint)Pdfium.FPDF_GetDocPermissions(Handle).Value;
            }
        }
    }

    public bool CanModify => Permissions.HasFlag(PdfPermissions.Modify) || Permissions.HasFlag(PdfPermissions.Assemble);

    public PdfPageInfo GetPageInfo(int index)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(index);
            Pdfium.FS_SIZEF size;
            if (Pdfium.FPDF_GetPageSizeByIndexF(Handle, index, &size) == 0)
            {
                throw PdfiumLibrary.LastError($"Seite {index + 1}");
            }
            return new PdfPageInfo(index, size.width, size.height, ReadPageLabel(index));
        }
    }

    /// <summary>Dauerhafte Drehung der Seite in Grad (0, 90, 180, 270). Lädt die Seite.</summary>
    public int GetPageRotation(int index)
    {
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(index);
            return Pdfium.FPDFPage_GetRotation(GetPage(index)) * 90;
        }
    }

    public IReadOnlyList<PdfPageInfo> GetPages()
    {
        lock (PdfiumLibrary.Sync)
        {
            var count = Pdfium.FPDF_GetPageCount(Handle);
            var pages = new PdfPageInfo[count];
            for (var i = 0; i < count; i++)
            {
                pages[i] = GetPageInfo(i);
            }
            return pages;
        }
    }

    public PdfMetadata GetMetadata()
    {
        lock (PdfiumLibrary.Sync)
        {
            return new PdfMetadata(
                Meta("Title"), Meta("Author"), Meta("Subject"), Meta("Keywords"), Meta("Creator"), Meta("Producer"),
                ParsePdfDate(Meta("CreationDate")), ParsePdfDate(Meta("ModDate")));
        }
    }

    public IReadOnlyList<PdfBookmark> GetBookmarks()
    {
        lock (PdfiumLibrary.Sync)
        {
            return ReadBookmarks(0, depth: 0);
        }
    }

    /// <summary>
    /// Rendert einen Ausschnitt einer Seite als BGRA (32 Bit, vormultipliziert, Zeilen von oben nach unten).
    /// </summary>
    /// <param name="scale">Pixel je Punkt. 1 entspricht 72 dpi, 96/72 entspricht 100 % auf einem Standardbildschirm.</param>
    /// <param name="region">Ausschnitt innerhalb der Seite in Pixeln bei diesem Maßstab.</param>
    public void Render(int pageIndex, double scale, PixelRect region, Span<byte> bgra, int stride, bool annotations = true)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            return;
        }
        if (stride < region.Width * 4 || bgra.Length < stride * region.Height)
        {
            throw new ArgumentException("Der Puffer ist zu klein für den Ausschnitt.", nameof(bgra));
        }
        lock (PdfiumLibrary.Sync)
        {
            CheckIndex(pageIndex);
            var (fullW, fullH) = PagePixelSize(pageIndex, scale);
            var page = GetPage(pageIndex);
            fixed (byte* buffer = bgra)
            {
                var bitmap = Pdfium.FPDFBitmap_CreateEx(region.Width, region.Height, Pdfium.FPDFBitmap_BGRA, buffer, stride);
                if (bitmap == 0)
                {
                    throw new PdfException(PdfErrorKind.Unknown, "Das Bild für die Seitendarstellung konnte nicht angelegt werden.");
                }
                try
                {
                    Pdfium.FPDFBitmap_FillRect(bitmap, 0, 0, region.Width, region.Height, 0xFFFFFFFF);
                    var flags = annotations ? Pdfium.FPDF_ANNOT : 0;
                    Pdfium.FPDF_RenderPageBitmap(bitmap, page, -region.X, -region.Y, fullW, fullH, 0, flags);
                }
                finally
                {
                    Pdfium.FPDFBitmap_Destroy(bitmap);
                }
            }
        }
    }

    /// <summary>Größe der ganzen Seite in Pixeln beim angegebenen Maßstab.</summary>
    public (int Width, int Height) PagePixelSize(int pageIndex, double scale)
    {
        var info = GetPageInfo(pageIndex);
        return (Math.Max(1, (int)Math.Round(info.WidthPt * scale)), Math.Max(1, (int)Math.Round(info.HeightPt * scale)));
    }

    // ---------- Seiten bearbeiten ----------

    /// <summary>Dreht eine Seite dauerhaft um Vielfache von 90 Grad im Uhrzeigersinn.</summary>
    public void RotatePage(int index, int quarterTurns)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanModify();
            CheckIndex(index);
            var page = GetPage(index);
            var rotation = ((Pdfium.FPDFPage_GetRotation(page) + quarterTurns) % 4 + 4) % 4;
            Pdfium.FPDFPage_SetRotation(page, rotation);
            IsModified = true;
        }
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    public void DeletePage(int index)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanModify();
            CheckIndex(index);
            ClosePages();
            Pdfium.FPDFPage_Delete(Handle, index);
            MarkStructureChanged();
        }
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Verschiebt eine Seite so, dass sie danach den Index <paramref name="to"/> hat.</summary>
    public void MovePage(int from, int to)
    {
        if (from == to)
        {
            return;
        }
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanModify();
            CheckIndex(from);
            CheckIndex(to);
            ClosePages();
            var indices = stackalloc int[1] { from };
            if (Pdfium.FPDF_MovePages(Handle, indices, new CULong(1), to) == 0)
            {
                throw new PdfException(PdfErrorKind.Page, $"Seite {from + 1} konnte nicht verschoben werden.");
            }
            MarkStructureChanged();
        }
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Fügt eine leere Seite ein. Größe in Punkt.</summary>
    public void InsertBlankPage(int index, double widthPt, double heightPt)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanModify();
            if (index < 0 || index > Pdfium.FPDF_GetPageCount(Handle))
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            ClosePages();
            var page = Pdfium.FPDFPage_New(Handle, index, widthPt, heightPt);
            if (page == 0)
            {
                throw PdfiumLibrary.LastError("Leere Seite einfügen");
            }
            Pdfium.FPDF_ClosePage(page);
            MarkStructureChanged();
        }
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Übernimmt Seiten aus einem anderen Dokument (Zusammenführen).</summary>
    /// <param name="pageRange">Bereich wie „1,3,5-7“ (1-basiert); null für alle Seiten.</param>
    public void ImportPages(PdfDocument source, string? pageRange, int insertAt)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanModify();
            if (insertAt < 0 || insertAt > Pdfium.FPDF_GetPageCount(Handle))
            {
                throw new ArgumentOutOfRangeException(nameof(insertAt));
            }
            ClosePages();
            var range = pageRange is null ? null : Encoding.ASCII.GetBytes(pageRange + "\0");
            fixed (byte* r = range)
            {
                if (Pdfium.FPDF_ImportPages(Handle, source.Handle, r, insertAt) == 0)
                {
                    throw new PdfException(PdfErrorKind.Page, "Die Seiten konnten nicht übernommen werden. Bitte den Seitenbereich prüfen.");
                }
            }
            MarkStructureChanged();
        }
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Schreibt die angegebenen Seiten in eine neue Datei (Aufteilen). Das Dokument selbst bleibt unverändert.</summary>
    public void ExtractPages(string pageRange, string targetPath)
    {
        using var part = CreateNew();
        part.ImportPages(this, pageRange, 0);
        part.SaveAs(targetPath);
    }

    /// <summary>Bearbeitet den Seiteninhalt (Linien, Flächen, Text). Änderungen werden mit <see cref="PdfContentWriter.Commit"/> übernommen.</summary>
    public PdfContentWriter EditContent(int pageIndex)
    {
        lock (PdfiumLibrary.Sync)
        {
            EnsureCanModify();
            CheckIndex(pageIndex);
            var page = Pdfium.FPDF_LoadPage(Handle, pageIndex);
            if (page == 0)
            {
                throw PdfiumLibrary.LastError($"Seite {pageIndex + 1}");
            }
            ClosePages();
            return new PdfContentWriter(this, page);
        }
    }

    internal nint Handle => _handle != 0 ? _handle : throw new ObjectDisposedException(nameof(PdfDocument));

    internal void MarkContentChanged() => IsModified = true;

    // ---------- Speichern ----------

    /// <summary>Speichert in die Datei, aus der das Dokument geladen wurde.</summary>
    public void Save()
    {
        if (FilePath is null)
        {
            throw new InvalidOperationException("Das Dokument hat noch keinen Dateipfad. Bitte „Speichern unter“ verwenden.");
        }
        SaveAs(FilePath);
    }

    /// <summary>
    /// Speichert unter dem angegebenen Pfad und lädt das Dokument von dort neu. Geschrieben wird zuerst in eine
    /// temporäre Datei im Zielordner, die erst nach erfolgreichem Schreiben die Zieldatei ersetzt.
    /// </summary>
    public void SaveAs(string path, PdfSaveMode? mode = null)
    {
        var fullPath = Path.GetFullPath(path);
        var effectiveMode = mode ?? DefaultSaveMode;
        var temp = Path.Combine(Path.GetDirectoryName(fullPath)!, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                WriteTo(stream, effectiveMode);
                stream.Flush(flushToDisk: true);
            }

            lock (PdfiumLibrary.Sync)
            {
                var samePath = FilePath is not null && string.Equals(FilePath, fullPath, StringComparison.OrdinalIgnoreCase);
                if (samePath)
                {
                    // Die Quelldatei ist offen. Unter Windows lässt sie sich erst nach dem Schließen ersetzen.
                    CloseHandleAndSource();
                }
                try
                {
                    File.Move(temp, fullPath, overwrite: true);
                }
                catch (Exception ex) when (samePath)
                {
                    // Die Zieldatei ist z. B. in einem anderen Programm geöffnet. Der gespeicherte Stand wird aus der
                    // temporären Datei geladen, damit keine Änderung verloren geht; das Dokument bleibt als geändert markiert.
                    var keep = new MemorySource(File.ReadAllBytes(temp));
                    _handle = keep.Load(Password);
                    _source = keep;
                    throw new PdfException(PdfErrorKind.File,
                        $"{Path.GetFileName(fullPath)} konnte nicht überschrieben werden, vermutlich ist die Datei in einem anderen Programm geöffnet. Die Änderungen sind nicht verloren; bitte erneut speichern oder „Speichern unter“ verwenden.", ex);
                }
                CloseHandleAndSource();
                var source = new FileSource(fullPath);
                _handle = source.Load(Password);
                _source = source;
                FilePath = fullPath;
                IsModified = false;
                _structureChanged = false;
            }
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
        PagesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Schreibt das Dokument in einen Datenstrom, ohne den Zustand des Dokuments zu ändern.</summary>
    public void WriteTo(Stream target, PdfSaveMode mode)
    {
        lock (PdfiumLibrary.Sync)
        {
            ClosePages();
            var handle = GCHandle.Alloc(target);
            try
            {
                var writer = new Pdfium.FileWriteWithTarget
                {
                    version = 1,
                    WriteBlock = &WriteBlock,
                    Target = GCHandle.ToIntPtr(handle),
                };
                var flags = mode == PdfSaveMode.Incremental && _source is not null ? Pdfium.FPDF_INCREMENTAL : Pdfium.FPDF_NO_INCREMENTAL;
                if (Pdfium.FPDF_SaveAsCopy(Handle, &writer, flags) == 0)
                {
                    throw new PdfException(PdfErrorKind.Unknown, "Das Dokument konnte nicht gespeichert werden.");
                }
            }
            finally
            {
                handle.Free();
            }
        }
    }

    /// <summary>Inkrementell, solange keine Seiten gelöscht, eingefügt oder verschoben wurden.</summary>
    public PdfSaveMode DefaultSaveMode => _structureChanged || _source is null ? PdfSaveMode.Rewrite : PdfSaveMode.Incremental;

    [UnmanagedCallersOnly]
    private static int WriteBlock(Pdfium.FileWriteWithTarget* self, void* data, CULong size)
    {
        try
        {
            var stream = (Stream)GCHandle.FromIntPtr(self->Target).Target!;
            stream.Write(new ReadOnlySpan<byte>(data, checked((int)size.Value)));
            return 1;
        }
        catch
        {
            return 0;
        }
    }

    public void Dispose()
    {
        lock (PdfiumLibrary.Sync)
        {
            CloseHandleAndSource();
        }
    }

    // ---------- intern ----------

    private void CloseHandleAndSource()
    {
        ClosePages();
        if (_handle != 0)
        {
            Pdfium.FPDF_CloseDocument(_handle);
            _handle = 0;
        }
        _source?.Dispose();
        _source = null;
    }

    private void MarkStructureChanged()
    {
        IsModified = true;
        _structureChanged = true;
    }

    private void EnsureCanModify()
    {
        if (!CanModify)
        {
            throw new PdfException(PdfErrorKind.Permission, PdfException.Describe(PdfErrorKind.Permission));
        }
    }

    private void CheckIndex(int index)
    {
        var count = Pdfium.FPDF_GetPageCount(Handle);
        if (index < 0 || index >= count)
        {
            throw new ArgumentOutOfRangeException(nameof(index), $"Seite {index + 1} gibt es nicht; das Dokument hat {count} Seiten.");
        }
    }

    /// <summary>Lädt eine Seite oder liefert sie aus dem Zwischenspeicher. Aufrufer hält die Sperre.</summary>
    private nint GetPage(int index)
    {
        for (var node = _pageCache.First; node is not null; node = node.Next)
        {
            if (node.Value.Index == index)
            {
                _pageCache.Remove(node);
                _pageCache.AddFirst(node);
                return node.Value.Page;
            }
        }
        var page = Pdfium.FPDF_LoadPage(Handle, index);
        if (page == 0)
        {
            throw PdfiumLibrary.LastError($"Seite {index + 1}");
        }
        _pageCache.AddFirst((index, page));
        if (_pageCache.Count > PageCacheSize)
        {
            Pdfium.FPDF_ClosePage(_pageCache.Last!.Value.Page);
            _pageCache.RemoveLast();
        }
        return page;
    }

    private void ClosePages()
    {
        foreach (var (_, page) in _pageCache)
        {
            Pdfium.FPDF_ClosePage(page);
        }
        _pageCache.Clear();
    }

    private string? Meta(string tag)
    {
        var tagBytes = Encoding.ASCII.GetBytes(tag + "\0");
        return ReadUtf16(Handle, (h, buf, len) =>
        {
            fixed (byte* t = tagBytes)
            {
                return Pdfium.FPDF_GetMetaText(h, t, buf, len);
            }
        });
    }

    private string? ReadPageLabel(int index) =>
        ReadUtf16(Handle, (h, buf, len) => Pdfium.FPDF_GetPageLabel(h, index, buf, len));

    private delegate CULong Utf16Getter(nint handle, void* buffer, CULong length);

    /// <summary>Liest eine UTF-16LE-Zeichenkette nach dem PDFium-Muster „erst Länge, dann Inhalt“.</summary>
    private static string? ReadUtf16(nint handle, Utf16Getter getter)
    {
        var length = (int)getter(handle, null, new CULong(0)).Value;
        if (length <= 2)
        {
            return null;
        }
        var buffer = new byte[length];
        fixed (byte* b = buffer)
        {
            getter(handle, b, new CULong((uint)length));
        }
        var text = Encoding.Unicode.GetString(buffer, 0, length - 2);
        return string.IsNullOrEmpty(text) ? null : text;
    }

    private IReadOnlyList<PdfBookmark> ReadBookmarks(nint parent, int depth)
    {
        var result = new List<PdfBookmark>();
        if (depth > 32)
        {
            return result;
        }
        var seen = new HashSet<nint>();
        for (var b = Pdfium.FPDFBookmark_GetFirstChild(Handle, parent); b != 0 && seen.Add(b); b = Pdfium.FPDFBookmark_GetNextSibling(Handle, b))
        {
            var bookmark = b;
            var title = ReadUtf16(Handle, (_, buf, len) => Pdfium.FPDFBookmark_GetTitle(bookmark, buf, len)) ?? "";
            var dest = Pdfium.FPDFBookmark_GetDest(Handle, b);
            if (dest == 0)
            {
                var action = Pdfium.FPDFBookmark_GetAction(b);
                dest = action != 0 ? Pdfium.FPDFAction_GetDest(Handle, action) : 0;
            }
            int? page = dest != 0 ? Pdfium.FPDFDest_GetDestPageIndex(Handle, dest) : null;
            result.Add(new PdfBookmark(title, page >= 0 ? page : null, ReadBookmarks(b, depth + 1)));
        }
        return result;
    }

    /// <summary>Wandelt ein PDF-Datum (D:YYYYMMDDHHmmSSOHH'mm') um. Liefert null bei unlesbaren Werten.</summary>
    public static DateTimeOffset? ParsePdfDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        var s = value.Trim();
        if (s.StartsWith("D:", StringComparison.Ordinal))
        {
            s = s[2..];
        }
        var digits = new string(s.TakeWhile(char.IsDigit).ToArray());
        if (digits.Length < 4)
        {
            return null;
        }
        int Part(int start, int len, int fallback) => digits.Length >= start + len ? int.Parse(digits.AsSpan(start, len), CultureInfo.InvariantCulture) : fallback;
        var offset = TimeSpan.Zero;
        var rest = s[digits.Length..];
        if (rest.Length > 0 && (rest[0] == '+' || rest[0] == '-'))
        {
            var tz = new string(rest[1..].Where(char.IsDigit).ToArray());
            if (tz.Length >= 2)
            {
                var hours = int.Parse(tz.AsSpan(0, 2), CultureInfo.InvariantCulture);
                var minutes = tz.Length >= 4 ? int.Parse(tz.AsSpan(2, 2), CultureInfo.InvariantCulture) : 0;
                offset = new TimeSpan(hours, minutes, 0) * (rest[0] == '-' ? -1 : 1);
            }
        }
        try
        {
            return new DateTimeOffset(Part(0, 4, 1), Part(4, 2, 1), Part(6, 2, 1), Part(8, 2, 0), Part(10, 2, 0), Part(12, 2, 0), offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    // ---------- Quellen ----------

    private interface IDocumentSource : IDisposable
    {
        nint Load(string? password);
    }

    /// <summary>Liest blockweise direkt aus der Datei, damit auch sehr große Pläne nicht ganz in den Speicher müssen.</summary>
    private sealed class FileSource : IDocumentSource
    {
        private readonly SafeFileHandle _file;
        private GCHandle _self;
        private Pdfium.FPDF_FILEACCESS* _access;
        private readonly string _name;

        public FileSource(string path)
        {
            _name = Path.GetFileName(path);
            _file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
        }

        public nint Load(string? password)
        {
            var length = RandomAccess.GetLength(_file);
            if (length > (OperatingSystem.IsWindows() ? uint.MaxValue : long.MaxValue))
            {
                _file.Dispose();
                throw new PdfException(PdfErrorKind.File, $"Öffnen von {_name}: Dateien über 4 GB werden derzeit nicht unterstützt.");
            }
            _self = GCHandle.Alloc(this);
            _access = (Pdfium.FPDF_FILEACCESS*)NativeMemory.AllocZeroed((nuint)sizeof(Pdfium.FPDF_FILEACCESS));
            _access->m_FileLen = new CULong((nuint)length);
            _access->m_GetBlock = &GetBlock;
            _access->m_Param = GCHandle.ToIntPtr(_self);
            lock (PdfiumLibrary.Sync)
            {
                var pw = password is null ? null : Encoding.UTF8.GetBytes(password + "\0");
                fixed (byte* p = pw)
                {
                    var handle = Pdfium.FPDF_LoadCustomDocument(_access, p);
                    if (handle == 0)
                    {
                        throw PdfiumLibrary.LastError($"Öffnen von {_name}");
                    }
                    return handle;
                }
            }
        }

        [UnmanagedCallersOnly]
        private static int GetBlock(nint param, CULong position, byte* buffer, CULong size)
        {
            try
            {
                var self = (FileSource)GCHandle.FromIntPtr(param).Target!;
                var span = new Span<byte>(buffer, checked((int)size.Value));
                var offset = (long)position.Value;
                while (span.Length > 0)
                {
                    var read = RandomAccess.Read(self._file, span, offset);
                    if (read <= 0)
                    {
                        return 0;
                    }
                    span = span[read..];
                    offset += read;
                }
                return 1;
            }
            catch
            {
                return 0;
            }
        }

        public void Dispose()
        {
            _file.Dispose();
            if (_access != null)
            {
                NativeMemory.Free(_access);
                _access = null;
            }
            if (_self.IsAllocated)
            {
                _self.Free();
            }
        }
    }

    private sealed class MemorySource : IDocumentSource
    {
        private void* _data;
        private readonly nuint _length;

        public MemorySource(ReadOnlySpan<byte> data)
        {
            _length = (nuint)data.Length;
            _data = NativeMemory.Alloc(Math.Max(_length, 1));
            data.CopyTo(new Span<byte>(_data, data.Length));
        }

        public nint Load(string? password)
        {
            lock (PdfiumLibrary.Sync)
            {
                var pw = password is null ? null : Encoding.UTF8.GetBytes(password + "\0");
                fixed (byte* p = pw)
                {
                    var handle = Pdfium.FPDF_LoadMemDocument64(_data, _length, p);
                    if (handle == 0)
                    {
                        throw PdfiumLibrary.LastError("Öffnen");
                    }
                    return handle;
                }
            }
        }

        public void Dispose()
        {
            if (_data != null)
            {
                NativeMemory.Free(_data);
                _data = null;
            }
        }
    }
}
