namespace Egle360.Design;

/// <summary>Eintrag der Hauptnavigation. Module registrieren sich hier erst, wenn sie funktionsfähig sind.</summary>
public sealed record NavigationEntry(string Id, string Title, string IconGlyph, NavigationGroup Group = NavigationGroup.Main);

public enum NavigationGroup
{
    Main,
    Footer,
}

/// <summary>Zustand der Hauptnavigation ohne UI-Abhängigkeit.</summary>
public sealed class NavigationModel
{
    private readonly List<NavigationEntry> _entries = new();

    public event EventHandler<NavigationEntry>? SelectionChanged;

    public IReadOnlyList<NavigationEntry> Entries => _entries;

    public NavigationEntry? Selected { get; private set; }

    public void Register(NavigationEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (_entries.Any(e => e.Id == entry.Id))
        {
            throw new InvalidOperationException($"Navigationseintrag '{entry.Id}' ist bereits registriert.");
        }
        _entries.Add(entry);
    }

    /// <summary>Wählt einen Eintrag aus. Liefert false, wenn die ID unbekannt ist.</summary>
    public bool Select(string id)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == id);
        if (entry is null)
        {
            return false;
        }
        if (!ReferenceEquals(entry, Selected))
        {
            Selected = entry;
            SelectionChanged?.Invoke(this, entry);
        }
        return true;
    }
}
