using System.Runtime.InteropServices;

namespace Egle360.Pdf.Native;

/// <summary>
/// Aufrufe in die PDFium-Bibliothek (BSD-3-Clause, Binärpaket bblanchon.PDFium, Apache-2.0).
/// Signaturen nach den Headern von PDFium 156.0.8076. PDFium ist nicht threadsicher:
/// Jeder Aufruf läuft unter <see cref="PdfiumLibrary.Sync"/>.
/// </summary>
/// <remarks>
/// <c>unsigned long</c> ist unter Windows 32 Bit und unter Linux 64 Bit breit, deshalb <see cref="CULong"/>.
/// </remarks>
internal static unsafe class Pdfium
{
    private const string Lib = "pdfium";

    public const int FPDF_ERR_SUCCESS = 0;
    public const int FPDF_ERR_UNKNOWN = 1;
    public const int FPDF_ERR_FILE = 2;
    public const int FPDF_ERR_FORMAT = 3;
    public const int FPDF_ERR_PASSWORD = 4;
    public const int FPDF_ERR_SECURITY = 5;
    public const int FPDF_ERR_PAGE = 6;

    public const int FPDF_ANNOT = 0x01;
    public const int FPDF_PRINTING = 0x800;
    public const int FPDFBitmap_BGRA = 4;

    public const uint FPDF_INCREMENTAL = 1 << 0;
    public const uint FPDF_NO_INCREMENTAL = 1 << 1;

    public const int FPDF_FILLMODE_NONE = 0;
    public const int FPDF_FILLMODE_ALTERNATE = 1;

    [StructLayout(LayoutKind.Sequential)]
    public struct FPDF_FILEACCESS
    {
        public CULong m_FileLen;
        public delegate* unmanaged<nint, CULong, byte*, CULong, int> m_GetBlock;
        public nint m_Param;
    }

    /// <summary>FPDF_FILEWRITE mit angehängtem Verweis auf das Ziel; PDFium übergibt den Zeiger auf die Struktur zurück.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct FileWriteWithTarget
    {
        public int version;
        public delegate* unmanaged<FileWriteWithTarget*, void*, CULong, int> WriteBlock;
        public nint Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct FS_SIZEF
    {
        public float width;
        public float height;
    }

    // Bibliothek
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDF_InitLibrary();
    [DllImport(Lib, ExactSpelling = true)] public static extern CULong FPDF_GetLastError();

    // Dokument
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDF_LoadCustomDocument(FPDF_FILEACCESS* access, byte* password);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDF_LoadMemDocument64(void* data, nuint size, byte* password);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDF_CreateNewDocument();
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDF_CloseDocument(nint document);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_GetPageCount(nint document);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_GetPageSizeByIndexF(nint document, int pageIndex, FS_SIZEF* size);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_GetFileVersion(nint document, int* fileVersion);
    [DllImport(Lib, ExactSpelling = true)] public static extern CULong FPDF_GetMetaText(nint document, byte* tag, void* buffer, CULong length);
    [DllImport(Lib, ExactSpelling = true)] public static extern CULong FPDF_GetPageLabel(nint document, int pageIndex, void* buffer, CULong length);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_GetSecurityHandlerRevision(nint document);
    [DllImport(Lib, ExactSpelling = true)] public static extern CULong FPDF_GetDocPermissions(nint document);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_SaveAsCopy(nint document, FileWriteWithTarget* fileWrite, uint flags);

    // Seiten
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDF_LoadPage(nint document, int pageIndex);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDF_ClosePage(nint page);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPage_GetRotation(nint page);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDFPage_SetRotation(nint page, int rotate);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFPage_New(nint document, int pageIndex, double width, double height);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDFPage_Delete(nint document, int pageIndex);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_MovePages(nint document, int* pageIndices, CULong length, int destPageIndex);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDF_ImportPages(nint dest, nint src, byte* pageRange, int index);

    // Rendern
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFBitmap_CreateEx(int width, int height, int format, void* firstScan, int stride);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFBitmap_FillRect(nint bitmap, int left, int top, int width, int height, uint color);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDFBitmap_Destroy(nint bitmap);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDF_RenderPageBitmap(nint bitmap, nint page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

    // Lesezeichen
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFBookmark_GetFirstChild(nint document, nint bookmark);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFBookmark_GetNextSibling(nint document, nint bookmark);
    [DllImport(Lib, ExactSpelling = true)] public static extern CULong FPDFBookmark_GetTitle(nint bookmark, void* buffer, CULong length);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFBookmark_GetDest(nint document, nint bookmark);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFBookmark_GetAction(nint bookmark);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFAction_GetDest(nint document, nint action);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFDest_GetDestPageIndex(nint document, nint dest);

    // Inhalte erzeugen
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFPageObj_CreateNewPath(float x, float y);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFPageObj_CreateNewRect(float x, float y, float w, float h);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPath_MoveTo(nint path, float x, float y);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPath_LineTo(nint path, float x, float y);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPath_Close(nint path);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPath_SetDrawMode(nint path, int fillMode, int stroke);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPageObj_SetStrokeColor(nint obj, uint r, uint g, uint b, uint a);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPageObj_SetFillColor(nint obj, uint r, uint g, uint b, uint a);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPageObj_SetStrokeWidth(nint obj, float width);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDFPageObj_Transform(nint obj, double a, double b, double c, double d, double e, double f);
    [DllImport(Lib, ExactSpelling = true)] public static extern nint FPDFPageObj_NewTextObj(nint document, byte* font, float fontSize);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFText_SetText(nint textObject, char* text);
    [DllImport(Lib, ExactSpelling = true)] public static extern void FPDFPage_InsertObject(nint page, nint obj);
    [DllImport(Lib, ExactSpelling = true)] public static extern int FPDFPage_GenerateContent(nint page);
}
