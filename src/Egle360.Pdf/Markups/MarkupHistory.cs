namespace Egle360.Pdf.Markups;

/// <summary>
/// Änderungen an Markierungen mit Rückgängig und Wiederholen. Jede Änderung speichert den Stand der Seite davor
/// und danach; das reicht, weil Markierungen unveränderlich sind.
/// </summary>
/// <remarks>
/// Nach dem Einfügen, Löschen oder Verschieben von Seiten passen die Seitenindizes nicht mehr. Der Verlauf wird
/// dann verworfen.
/// </remarks>
public sealed class MarkupHistory(PdfDocument document, ITextOutliner outliner, int capacity = 200)
{
    private readonly LinkedList<Entry> _undo = new();
    private readonly Stack<Entry> _redo = new();

    private sealed record Entry(int Page, IReadOnlyList<Markup> Before, IReadOnlyList<Markup> After, string Description, int Version);

    public PdfDocument Document { get; } = document;

    public ITextOutliner Outliner { get; } = outliner;

    /// <summary>Wird nach jeder Änderung, jedem Rückgängig und Wiederholen ausgelöst (Argument: Seitenindex).</summary>
    public event EventHandler<int>? Changed;

    public bool CanUndo => Valid() && _undo.Count > 0;

    public bool CanRedo => Valid() && _redo.Count > 0;

    public string? UndoDescription => CanUndo ? _undo.Last!.Value.Description : null;

    public string? RedoDescription => CanRedo ? _redo.Peek().Description : null;

    public IReadOnlyList<Markup> Get(int page) => Document.GetMarkups(page);

    /// <summary>Setzt den neuen Stand einer Seite und merkt sich den alten.</summary>
    public void Apply(int page, IReadOnlyList<Markup> after, string description)
    {
        Valid();
        var before = Document.GetMarkups(page);
        Document.SetMarkups(page, after, Outliner);
        _undo.AddLast(new Entry(page, before, after.ToArray(), description, Document.StructureVersion));
        if (_undo.Count > capacity)
        {
            _undo.RemoveFirst();
        }
        _redo.Clear();
        Changed?.Invoke(this, page);
    }

    public void Add(int page, Markup markup) =>
        Apply(page, [.. Get(page), markup], $"{MarkupKinds.DisplayName(markup.Kind)} hinzufügen");

    public void Replace(int page, Markup markup, string? description = null) =>
        Apply(page, Get(page).Select(m => m.Id == markup.Id ? markup : m).ToArray(), description ?? $"{MarkupKinds.DisplayName(markup.Kind)} ändern");

    public void ReplaceMany(int page, IReadOnlyCollection<Markup> markups, string description)
    {
        var map = markups.ToDictionary(m => m.Id);
        Apply(page, Get(page).Select(m => map.GetValueOrDefault(m.Id, m)).ToArray(), description);
    }

    public void Remove(int page, IReadOnlyCollection<string> ids)
    {
        var current = Get(page);
        var removed = current.Where(m => ids.Contains(m.Id)).ToList();
        if (removed.Count == 0)
        {
            return;
        }
        var description = removed.Count == 1 ? $"{MarkupKinds.DisplayName(removed[0].Kind)} löschen" : $"{removed.Count} Markierungen löschen";
        Apply(page, current.Where(m => !ids.Contains(m.Id)).ToArray(), description);
    }

    /// <summary>Macht die letzte Änderung rückgängig. Liefert die betroffene Seite oder null.</summary>
    public int? Undo()
    {
        if (!CanUndo)
        {
            return null;
        }
        var entry = _undo.Last!.Value;
        _undo.RemoveLast();
        Document.SetMarkups(entry.Page, entry.Before, Outliner);
        _redo.Push(entry);
        Changed?.Invoke(this, entry.Page);
        return entry.Page;
    }

    public int? Redo()
    {
        if (!CanRedo)
        {
            return null;
        }
        var entry = _redo.Pop();
        Document.SetMarkups(entry.Page, entry.After, Outliner);
        _undo.AddLast(entry);
        Changed?.Invoke(this, entry.Page);
        return entry.Page;
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
    }

    private bool Valid()
    {
        var version = Document.StructureVersion;
        if ((_undo.Last is { } last && last.Value.Version != version) || (_redo.Count > 0 && _redo.Peek().Version != version))
        {
            Clear();
        }
        return true;
    }
}
