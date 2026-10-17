using Egle360.Pdf;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Controls;

/// <summary>Hervorhebung der Fundstellen einer Textsuche.</summary>
public sealed partial class PdfViewer
{
    private readonly Dictionary<int, List<PdfRect>> _searchHits = new();
    private PdfTextMatch? _currentMatch;

    /// <summary>Ersetzt alle hervorgehobenen Fundstellen.</summary>
    public void SetSearchMatches(IEnumerable<PdfTextMatch> matches)
    {
        _searchHits.Clear();
        foreach (var m in matches)
        {
            if (!_searchHits.TryGetValue(m.Page, out var list))
            {
                _searchHits[m.Page] = list = [];
            }
            list.AddRange(m.Rects);
        }
        RefreshSearchLayers();
    }

    /// <summary>Hebt eine Fundstelle besonders hervor und zeigt sie mittig an.</summary>
    public void ShowMatch(PdfTextMatch? match)
    {
        _currentMatch = match;
        RefreshSearchLayers();
        if (match is { Rects.Count: > 0 })
        {
            var r = match.Rects[0];
            CenterOn(match.Page, new PdfPoint((r.Left + r.Right) / 2, (r.Bottom + r.Top) / 2));
        }
        else if (match is not null)
        {
            GoToPage(match.Page);
        }
    }

    public void ClearSearch()
    {
        _searchHits.Clear();
        _currentMatch = null;
        RefreshSearchLayers();
    }

    private void RefreshSearchLayers()
    {
        foreach (var (index, visual) in _visuals)
        {
            ApplySearch(visual, index);
        }
    }

    private void ApplySearch(PageVisual visual, int index) =>
        visual.Layer.SetSearchHits(
            _searchHits.TryGetValue(index, out var hits) ? hits : [],
            _currentMatch is { } m && m.Page == index ? m.Rects : []);
}
