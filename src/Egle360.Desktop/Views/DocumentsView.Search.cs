using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Egle360.Pdf;

namespace Egle360.Desktop.Views;

/// <summary>Textsuche über alle Seiten des aktiven Dokuments.</summary>
public partial class DocumentsView
{
    private readonly ObservableCollection<SearchItem> _searchItems = [];
    private CancellationTokenSource? _searchCancel;

    private sealed record SearchItem(PdfTextMatch Match, string PageLabel, string Context);

    /// <summary>Öffnet die Suche in der Seitenleiste und setzt den Fokus in das Suchfeld (Strg+F).</summary>
    private void ShowSearch()
    {
        if (_active is null)
        {
            return;
        }
        if (Sidebar.Visibility != Visibility.Visible)
        {
            OnToggleSidebar(this, new RoutedEventArgs());
        }
        SearchTab.IsChecked = true;
        Dispatcher.BeginInvoke(() =>
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
        });
    }

    private void OnSearchBoxKey(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (_searchItems.Count > 0 && SearchBox.Text == _lastQuery)
            {
                StepSearch(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1);
            }
            else
            {
                _ = SearchAsync(SearchBox.Text);
            }
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ResetSearch();
            _active?.Viewer.FocusView();
            e.Handled = true;
        }
    }

    private string? _lastQuery;

    /// <summary>Durchsucht alle Seiten des aktiven Dokuments; Treffer erscheinen nach und nach in der Liste.</summary>
    internal async Task SearchAsync(string query)
    {
        if (SearchBox.Text != query)
        {
            SearchBox.Text = query;
        }
        ResetSearch();
        if (_active is not { } open || string.IsNullOrWhiteSpace(query))
        {
            return;
        }
        _lastQuery = query;
        if (SearchTab.IsChecked != true)
        {
            ShowSearch();
        }
        var cancel = _searchCancel = new CancellationTokenSource();
        var document = open.Document;
        var pageCount = document.PageCount;
        var found = new List<PdfTextMatch>();
        var pagesWithText = 0;
        SearchResults.ItemsSource = _searchItems;
        try
        {
            for (var page = 0; page < pageCount; page++)
            {
                SearchStatus.Text = $"Suche auf Seite {page + 1} von {pageCount} …";
                var p = page;
                var (matches, hasText) = await Task.Run(() =>
                {
                    var m = document.FindText(p, query);
                    return (m, m.Count > 0 || document.GetPageText(p).Trim().Length > 0);
                }, cancel.Token);
                if (cancel.IsCancellationRequested)
                {
                    return;
                }
                pagesWithText += hasText ? 1 : 0;
                foreach (var m in matches)
                {
                    found.Add(m);
                    _searchItems.Add(new SearchItem(m, $"Seite {m.Page + 1}", m.Context));
                }
                if (matches.Count > 0)
                {
                    open.Viewer.SetSearchMatches(found);
                    if (found.Count == matches.Count)
                    {
                        SearchResults.SelectedIndex = 0;
                    }
                }
            }
            var pages = found.Select(m => m.Page).Distinct().Count();
            SearchStatus.Text = found.Count switch
            {
                0 when pagesWithText == 0 => "Kein durchsuchbarer Text gefunden. Das Dokument ist vermutlich gescannt oder enthält Text nur als Linien.",
                0 => $"„{query.Trim()}“ kommt im Dokument nicht vor.",
                1 => "1 Fundstelle. Eingabetaste oder F3 springt weiter.",
                _ => $"{found.Count} Fundstellen auf {pages} {(pages == 1 ? "Seite" : "Seiten")}. Eingabetaste oder F3 springt weiter.",
            };
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex) when (ex is PdfException or ObjectDisposedException or ArgumentOutOfRangeException)
        {
            SearchStatus.Text = $"Die Suche wurde abgebrochen: {ex.Message}";
        }
    }

    /// <summary>Für den Screenshot-Modus: Suche beenden und wieder die Seitenminiaturen zeigen.</summary>
    internal void EndSearchForScreenshot()
    {
        ResetSearch();
        SearchBox.Text = "";
        PagesTab.IsChecked = true;
    }

    /// <summary>Springt zur nächsten oder vorherigen Fundstelle (F3, Umschalt+F3).</summary>
    private void StepSearch(int direction)
    {
        if (_searchItems.Count == 0)
        {
            ShowSearch();
            return;
        }
        var i = SearchResults.SelectedIndex < 0 ? 0 : (SearchResults.SelectedIndex + direction + _searchItems.Count) % _searchItems.Count;
        SearchResults.SelectedIndex = i;
        SearchResults.ScrollIntoView(_searchItems[i]);
    }

    private void OnSearchResultSelected(object sender, SelectionChangedEventArgs e)
    {
        if (SearchResults.SelectedItem is SearchItem item)
        {
            _active?.Viewer.ShowMatch(item.Match);
        }
    }

    /// <summary>Bricht eine laufende Suche ab und entfernt alle Hervorhebungen.</summary>
    private void ResetSearch()
    {
        _searchCancel?.Cancel();
        _searchCancel = null;
        _lastQuery = null;
        _searchItems.Clear();
        foreach (var d in _documents)
        {
            d.Viewer.ClearSearch();
        }
        if (SearchStatus is not null)
        {
            SearchStatus.Text = "Begriff eingeben und mit der Eingabetaste suchen.";
        }
    }
}
