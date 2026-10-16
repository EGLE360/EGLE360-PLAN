namespace Egle360.Pdf.Tests;

/// <summary>Zugriff auf die Testdateien (erzeugt mit tools/make_pdf_fixtures.py) und temporäre Arbeitskopien.</summary>
internal static class Fixtures
{
    public static string PathOf(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    /// <summary>Kopiert eine Testdatei in einen eigenen temporären Ordner, damit Tests sie verändern dürfen.</summary>
    public static string WorkingCopy(string name, TempDir dir)
    {
        var target = Path.Combine(dir.Path, name);
        File.Copy(PathOf(name), target);
        return target;
    }

    /// <summary>Liest ein Pixel (B, G, R, A) aus einem BGRA-Puffer.</summary>
    public static (byte B, byte G, byte R, byte A) Pixel(byte[] bgra, int stride, int x, int y)
    {
        var i = y * stride + x * 4;
        return (bgra[i], bgra[i + 1], bgra[i + 2], bgra[i + 3]);
    }

    public static bool IsBlack(this (byte B, byte G, byte R, byte A) p) => p.B < 40 && p.G < 40 && p.R < 40;

    public static bool IsWhite(this (byte B, byte G, byte R, byte A) p) => p.B > 215 && p.G > 215 && p.R > 215;

    /// <summary>Rendert eine ganze Seite und gibt Puffer, Breite, Höhe zurück.</summary>
    public static (byte[] Buffer, int Width, int Height) RenderFull(PdfDocument doc, int page, double scale)
    {
        var (w, h) = doc.PagePixelSize(page, scale);
        var buffer = new byte[w * h * 4];
        doc.Render(page, scale, new PixelRect(0, 0, w, h), buffer, w * 4);
        return (buffer, w, h);
    }
}

internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "egle360-pdf-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string name) => System.IO.Path.Combine(Path, name);

    public void Dispose()
    {
        try
        {
            Directory.Delete(Path, recursive: true);
        }
        catch (IOException)
        {
            // Unter Windows kann ein Virenscanner die Datei kurz sperren; der Ordner liegt im Temp-Verzeichnis.
        }
    }
}
