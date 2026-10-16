using Egle360.Pdf.Native;

namespace Egle360.Pdf;

/// <summary>
/// Initialisiert PDFium einmal pro Prozess und stellt die gemeinsame Sperre bereit.
/// PDFium darf nicht von mehreren Threads gleichzeitig benutzt werden.
/// </summary>
internal static class PdfiumLibrary
{
    public static readonly Lock Sync = new();
    private static bool _initialized;

    public static void EnsureInitialized()
    {
        lock (Sync)
        {
            if (_initialized)
            {
                return;
            }
            Pdfium.FPDF_InitLibrary();
            _initialized = true;
        }
    }

    /// <summary>Wandelt den letzten PDFium-Fehler in eine Ausnahme mit deutscher Meldung um.</summary>
    public static PdfException LastError(string action)
    {
        var code = (int)Pdfium.FPDF_GetLastError().Value;
        var kind = code switch
        {
            Pdfium.FPDF_ERR_FILE => PdfErrorKind.File,
            Pdfium.FPDF_ERR_FORMAT => PdfErrorKind.Format,
            Pdfium.FPDF_ERR_PASSWORD => PdfErrorKind.Password,
            Pdfium.FPDF_ERR_SECURITY => PdfErrorKind.Security,
            Pdfium.FPDF_ERR_PAGE => PdfErrorKind.Page,
            _ => PdfErrorKind.Unknown,
        };
        return new PdfException(kind, $"{action}: {PdfException.Describe(kind)}");
    }
}

public enum PdfErrorKind
{
    Unknown,
    File,
    Format,
    Password,
    Security,
    Page,
    Permission,
}

public sealed class PdfException(PdfErrorKind kind, string message, Exception? inner = null) : Exception(message, inner)
{
    public PdfErrorKind Kind { get; } = kind;

    public static string Describe(PdfErrorKind kind) => kind switch
    {
        PdfErrorKind.File => "Die Datei wurde nicht gefunden oder kann nicht gelesen werden.",
        PdfErrorKind.Format => "Die Datei ist kein gültiges PDF oder beschädigt.",
        PdfErrorKind.Password => "Das Dokument ist geschützt. Das Kennwort fehlt oder ist falsch.",
        PdfErrorKind.Security => "Das Verschlüsselungsverfahren dieses Dokuments wird nicht unterstützt.",
        PdfErrorKind.Page => "Die Seite fehlt oder ihr Inhalt ist fehlerhaft.",
        PdfErrorKind.Permission => "Die Berechtigungen des Dokuments erlauben diese Änderung nicht.",
        _ => "Unbekannter Fehler in der PDF-Bibliothek.",
    };
}
