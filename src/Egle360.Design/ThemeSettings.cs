using System.Text.Json;
using System.Text.Json.Serialization;

namespace Egle360.Design;

public enum ThemeKind
{
    Light,
    Dark,
}

/// <summary>Wahl des Benutzers. <see cref="System"/> folgt der Windows-Einstellung.</summary>
public enum ThemePreference
{
    System,
    Light,
    Dark,
}

public static class ThemeResolver
{
    public static ThemeKind Resolve(ThemePreference preference, bool systemUsesLightTheme) => preference switch
    {
        ThemePreference.Light => ThemeKind.Light,
        ThemePreference.Dark => ThemeKind.Dark,
        _ => systemUsesLightTheme ? ThemeKind.Light : ThemeKind.Dark,
    };
}

/// <summary>Benutzereinstellungen der Oberfläche, gespeichert als JSON.</summary>
public sealed class UiSettings
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public bool NavigationCollapsed { get; set; }

    /// <summary>Zuletzt geöffnete Dokumente, neuestes zuerst.</summary>
    public List<string> RecentDocuments { get; set; } = [];

    /// <summary>Breite der Seitenleiste in der Dokumentansicht.</summary>
    public double DocumentSidebarWidth { get; set; } = 220;
}

/// <summary>Pflege der Liste zuletzt geöffneter Dokumente.</summary>
public static class RecentDocuments
{
    public const int MaxEntries = 10;

    /// <summary>Setzt den Pfad an die erste Stelle, entfernt Doppelte (ohne Groß-/Kleinschreibung) und kürzt die Liste.</summary>
    public static void Add(List<string> list, string path)
    {
        var full = Path.GetFullPath(path);
        list.RemoveAll(p => string.Equals(p, full, StringComparison.OrdinalIgnoreCase));
        list.Insert(0, full);
        if (list.Count > MaxEntries)
        {
            list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        }
    }

    /// <summary>Entfernt Einträge, deren Datei nicht mehr existiert. Liefert die Anzahl entfernter Einträge.</summary>
    public static int RemoveMissing(List<string> list, Func<string, bool>? exists = null)
    {
        exists ??= File.Exists;
        return list.RemoveAll(p => !exists(p));
    }
}

public sealed class UiSettingsStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _path;

    public UiSettingsStore(string path) => _path = path;

    /// <summary>Standardpfad unter %APPDATA%\EGLE360\PLAN.</summary>
    public static UiSettingsStore CreateDefault() => new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EGLE360", "PLAN", "ui-settings.json"));

    /// <summary>Liest die Einstellungen. Fehlende oder beschädigte Dateien ergeben die Standardwerte.</summary>
    public UiSettings Load()
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new UiSettings();
            }
            return JsonSerializer.Deserialize<UiSettings>(File.ReadAllText(_path), Options) ?? new UiSettings();
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return new UiSettings();
        }
    }

    /// <summary>Schreibt atomar über eine temporäre Datei, damit ein Absturz keine halbe Datei hinterlässt.</summary>
    public void Save(UiSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var tmp = _path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(settings, Options));
        File.Move(tmp, _path, overwrite: true);
    }
}
