using System.IO;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Services;

/// <summary>
/// Werkzeugbibliothek des Benutzers in %APPDATA%\EGLE360\PLAN\Werkzeuge.json. Ohne Datei gilt die Grundausstattung.
/// Eine unlesbare Datei wird zur Sicherung umbenannt statt überschrieben.
/// </summary>
public sealed class ToolLibraryStore
{
    private readonly string? _path;

    /// <param name="path">Speicherort; null hält die Bibliothek nur im Speicher (Screenshot-Modus).</param>
    public ToolLibraryStore(string? path) => _path = path;

    public static ToolLibraryStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EGLE360", "PLAN", "Werkzeuge.json"));

    public ToolLibrary Library { get; private set; } = ToolLibrary.Defaults();

    public event EventHandler? Changed;

    public void Load()
    {
        if (_path is null || !File.Exists(_path))
        {
            return;
        }
        try
        {
            Library = ToolLibrary.FromJson(File.ReadAllText(_path));
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Move(_path, _path + $".defekt-{DateTime.Now:yyyyMMdd-HHmmss}", overwrite: true);
            }
            catch (Exception moveError) when (moveError is IOException or UnauthorizedAccessException)
            {
                // Die Grundausstattung gilt trotzdem; beim nächsten Speichern wird die Datei ersetzt.
            }
        }
    }

    /// <summary>Übernimmt eine geänderte Bibliothek und speichert sie atomar.</summary>
    public void Update(ToolLibrary library)
    {
        Library = library;
        if (_path is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, library.ToJson());
            File.Move(tmp, _path, overwrite: true);
        }
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
