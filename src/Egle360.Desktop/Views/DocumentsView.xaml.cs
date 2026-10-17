using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using Egle360.Desktop.Controls;
using Egle360.Desktop.Services;
using Egle360.Design;
using Egle360.Pdf;
using Egle360.Pdf.Markups;
using Microsoft.Win32;

namespace Egle360.Desktop.Views;

/// <summary>
/// Dokumentbereich: mehrere PDFs als Registerkarten, Seitenansicht mit Zoom, Miniaturen, Lesezeichen und
/// Seitenbearbeitung (Drehen, Löschen, Verschieben, Einfügen, Zusammenführen, Aufteilen).
/// </summary>
public partial class DocumentsView : UserControl, IDisposable
{
    private const string PdfFilter = "PDF-Dokumente (*.pdf)|*.pdf";
    private const double ThumbnailBox = 140;

    private readonly PageRenderService _renderer = new();
    private readonly List<OpenDocument> _documents = [];
    private OpenDocument? _active;
    private int _untitledCounter;
    private bool _syncingSelection;

    public DocumentsView()
    {
        InitializeComponent();
        BuildMarkupBar();
        InitToolLibrary();
        SidebarColumn.Width = new GridLength(Math.Clamp(App.Theme.Settings.DocumentSidebarWidth, 140, 480));
        PreviewKeyDown += OnPreviewKeyDown;
        RefreshRecent();
        RefreshUi();
    }

    /// <summary>Das aktive Dokument, für den Screenshot-Modus.</summary>
    internal PdfViewer? ActiveViewer => _active?.Viewer;

    internal PdfDocument? ActiveDocument => _active?.Document;

    /// <summary>
    /// Trägt Markierungen in ein geöffnetes Dokument ein, rückgängig machbar. False, wenn die Datei nicht geöffnet ist.
    /// </summary>
    internal bool TryAddMarkups(string path, int page, IReadOnlyList<Markup> markups, string description)
    {
        var full = Path.GetFullPath(path);
        if (_documents.FirstOrDefault(d => string.Equals(d.Document.FilePath, full, StringComparison.OrdinalIgnoreCase)) is not { } open
            || page >= open.Document.PageCount)
        {
            return false;
        }
        open.History.Apply(page, [.. open.Document.GetMarkups(page), .. markups], description);
        return true;
    }

    internal bool IsBusy => _renderer.Pending > 0 || _documents.Any(d => d.Viewer.IsBusy);

    private Window Owner => Window.GetWindow(this)!;

    // ---------- Öffnen und Schließen ----------

    public void OpenFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            OpenPath(path);
        }
    }

    private void OpenPath(string path)
    {
        var full = Path.GetFullPath(path);
        if (_documents.FirstOrDefault(d => string.Equals(d.Document.FilePath, full, StringComparison.OrdinalIgnoreCase)) is { } existing)
        {
            Activate(existing);
            return;
        }
        var document = TryOpen(full);
        if (document is null)
        {
            return;
        }
        RecentDocuments.Add(App.Theme.Settings.RecentDocuments, full);
        App.Theme.SaveSettings();
        RefreshRecent();
        Activate(AddDocument(document, Path.GetFileName(full)));
    }

    private PdfDocument? TryOpen(string path)
    {
        string? password = null;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return PdfDocument.Open(path, password);
            }
            catch (PdfException ex) when (ex.Kind == PdfErrorKind.Password && attempt < 3)
            {
                password = Dialogs.AskPassword(Owner, Path.GetFileName(path), retry: attempt > 0);
                if (password is null)
                {
                    return null;
                }
            }
            catch (PdfException ex)
            {
                ShowError(ex.Message);
                if (ex.Kind == PdfErrorKind.File)
                {
                    RecentDocuments.RemoveMissing(App.Theme.Settings.RecentDocuments);
                    App.Theme.SaveSettings();
                    RefreshRecent();
                }
                return null;
            }
        }
    }

    private OpenDocument AddDocument(PdfDocument document, string title)
    {
        var viewer = new PdfViewer { Outliner = _outliner, StyleProvider = CurrentStyle, TextProvider = kind => kind == MarkupKind.Stamp ? _stamp.Title : "", Author = Environment.UserName, SnapEnabled = _snap };
        var history = new MarkupHistory(document, _outliner);
        var open = new OpenDocument(document, viewer, history, title);
        open.Tab = CreateTab(open);
        viewer.Attach(document, _renderer, history);
        ApplyTool(viewer, CheckedToolKey);
        history.Changed += (_, page) =>
        {
            UpdateTab(open);
            RefreshThumbnail(open, page);
            if (open == _active)
            {
                RefreshMarkupBar();
                RefreshMarkupList();
            }
        };
        viewer.SelectionChanged += (_, _) => { if (open == _active) { RefreshMarkupBar(); } };
        viewer.ToolChanged += (_, _) => { if (open == _active) { SyncToolButtons(viewer); SyncToolList(); } };
        viewer.EditTextRequested += (_, args) => OnEditText(open, args.Page, args.Markup);
        viewer.DepthRequested += (_, args) => OnEditDepth(open, args.Page, args.Markup);
        viewer.SlopeRequested += (_, args) => OnEditSlope(open, args.Page, args.Markup);
        viewer.PriceRequested += (_, args) => OnEditPrice(open, args.Page, args.Markup);
        viewer.SaveAsToolRequested += (_, markup) => OnSaveAsTool(markup);
        viewer.RotateAngleRequested += (_, _) => OnRotateAngle(viewer);
        viewer.ViewHistoryChanged += (_, _) => { if (open == _active) { RefreshViewHistory(); } };
        viewer.MarkupsVisibilityChanged += (_, _) => { if (open == _active) { RefreshMarkupBar(); } };
        AttachMeasure(open);
        viewer.CurrentPageChanged += (_, _) => { if (open == _active) { SyncCurrentPage(); } };
        viewer.ZoomChanged += (_, _) => { if (open == _active) { RefreshZoom(); } };
        document.PagesChanged += (_, _) =>
        {
            if (open == _active)
            {
                ResetSearch();
            }
            viewer.Reload();
            open.Title = document.FilePath is { } p ? Path.GetFileName(p) : open.Title;
            UpdateTab(open);
            if (open == _active)
            {
                BuildThumbnails();
                BuildBookmarks();
                RefreshMarkupList();
                RefreshUi();
            }
        };
        _documents.Add(open);
        TabStrip.Children.Add(open.Tab);
        return open;
    }

    private void Activate(OpenDocument document)
    {
        _active?.Viewer.CancelDrawing();
        if (_active != document)
        {
            ResetSearch();
        }
        _active = document;
        document.Tab.IsChecked = true;
        ApplyTool(document.Viewer, CheckedToolKey);
        ViewerHost.Content = document.Viewer;
        RefreshMarkupList();
        BuildThumbnails();
        BuildBookmarks();
        RefreshUi();
        Dispatcher.BeginInvoke(document.Viewer.FocusView);
    }

    /// <summary>Schließt ein Dokument nach Rückfrage bei ungespeicherten Änderungen. Liefert false bei Abbruch.</summary>
    private bool Close(OpenDocument document)
    {
        if (document.Document.IsModified)
        {
            Activate(document);
            var answer = MessageBox.Show(Owner, $"Änderungen an „{document.Title}“ speichern?", "EGLE360 PLAN",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (answer == MessageBoxResult.Cancel || (answer == MessageBoxResult.Yes && !Save(document, saveAs: false)))
            {
                return false;
            }
        }
        var index = _documents.IndexOf(document);
        _documents.Remove(document);
        TabStrip.Children.Remove(document.Tab);
        document.Viewer.Detach();
        document.Document.Dispose();
        if (_active == document)
        {
            _active = null;
            ViewerHost.Content = null;
            if (_documents.Count > 0)
            {
                Activate(_documents[Math.Min(index, _documents.Count - 1)]);
            }
        }
        RefreshUi();
        return true;
    }

    /// <summary>Für den Screenshot-Modus: geänderte (temporäre) Dateien speichern, damit beim Beenden keine Rückfrage kommt.</summary>
    /// <summary>Für den Screenshot-Modus: Lage und Größe der Hauptbereiche als Text, um Layoutfehler zu finden.</summary>
    internal string DescribeLayoutForScreenshot()
    {
        string Line(string name, FrameworkElement e)
        {
            var p = e.IsVisible ? e.TranslatePoint(new Point(0, 0), this) : new Point(double.NaN, double.NaN);
            return $"{name}: sichtbar={e.IsVisible} oben={p.Y:0} links={p.X:0} breite={e.ActualWidth:0} höhe={e.ActualHeight:0}";
        }
        return string.Join(Environment.NewLine,
            Line("DocumentsView", this), Line("Menüband", MarkupBarBorder), Line("Registerkarten", TabStripBorder), Line("Arbeitsbereich", WorkArea),
            Line("Ansicht", ViewerHost), Line("Liste", MarkupListPanel), Line("Seitenleiste", Sidebar), Line("Leer", EmptyState),
            $"Zeilen: {string.Join(", ", ((Grid)Content).RowDefinitions.Select(r => r.ActualHeight.ToString("0")))}",
            $"Listenzeile: {MarkupListRow.ActualHeight:0}");
    }

    internal void SaveAllForScreenshot()
    {
        foreach (var document in _documents.Where(d => d.Document.IsModified && d.Document.FilePath is not null))
        {
            document.Document.Save();
        }
    }

    /// <summary>Für das Schließen des Hauptfensters. Liefert false, wenn der Benutzer abbricht.</summary>
    public bool ConfirmCloseAll()
    {
        foreach (var document in _documents.ToList())
        {
            if (!Close(document))
            {
                return false;
            }
        }
        return true;
    }

    public void Dispose()
    {
        foreach (var document in _documents)
        {
            document.Document.Dispose();
        }
        _renderer.Dispose();
    }

    private bool Save(OpenDocument document, bool saveAs)
    {
        var path = document.Document.FilePath;
        if (saveAs || path is null)
        {
            var dialog = new SaveFileDialog
            {
                Filter = PdfFilter,
                FileName = path is null ? document.Title + ".pdf" : Path.GetFileName(path),
                InitialDirectory = path is null ? null : Path.GetDirectoryName(path),
                AddExtension = true,
                DefaultExt = ".pdf",
            };
            if (dialog.ShowDialog(Owner) != true)
            {
                return false;
            }
            path = dialog.FileName;
        }
        try
        {
            document.Document.SaveAs(path);
            RecentDocuments.Add(App.Theme.Settings.RecentDocuments, path);
            App.Theme.SaveSettings();
            RefreshRecent();
            return true;
        }
        catch (Exception ex) when (ex is PdfException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
            UpdateTab(document);
            return false;
        }
    }

    // ---------- Registerkarten ----------

    private RadioButton CreateTab(OpenDocument document)
    {
        var title = new TextBlock { VerticalAlignment = VerticalAlignment.Center, MaxWidth = 260, TextTrimming = TextTrimming.CharacterEllipsis };
        var close = new Button
        {
            Style = (Style)FindResource("ToolButton"),
            Content = "",
            FontSize = 10,
            Width = 24,
            Height = 24,
            Margin = new Thickness(6, 0, 0, 0),
            ToolTip = "Schließen (Strg+W)",
        };
        AutomationPropertiesHelper.SetName(close, "Dokument schließen");
        close.Click += (_, _) => Close(document);
        var content = new StackPanel { Orientation = Orientation.Horizontal };
        content.Children.Add(title);
        content.Children.Add(close);
        var tab = new RadioButton { Style = (Style)FindResource("DocumentTab"), GroupName = "DocumentTabs", Content = content };
        tab.Checked += (_, _) => { if (_active != document) { Activate(document); } };
        tab.MouseUp += (_, e) => { if (e.ChangedButton == MouseButton.Middle) { Close(document); } };
        document.TabTitle = title;
        document.Tab = tab;
        UpdateTab(document);
        return tab;
    }

    private static void UpdateTab(OpenDocument document)
    {
        document.TabTitle.Text = document.Document.IsModified ? document.Title + " *" : document.Title;
        document.Tab.ToolTip = document.Document.FilePath ?? "Noch nicht gespeichert";
        AutomationPropertiesHelper.SetName(document.Tab, document.TabTitle.Text);
    }

    // ---------- Seitenleiste ----------

    private void BuildThumbnails()
    {
        if (_active is null)
        {
            Thumbnails.ItemsSource = null;
            return;
        }
        var items = _active.Document.GetPages().Select(p =>
        {
            var s = Math.Min(ThumbnailBox / p.WidthPt, ThumbnailBox / p.HeightPt);
            return new ThumbnailItem(p.Index, p.DisplayLabel, p.WidthPt * s, p.HeightPt * s);
        }).ToList();
        _active.Thumbnails = items;
        Thumbnails.ItemsSource = items;
        SyncCurrentPage();
    }

    private void OnThumbnailLoaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ThumbnailItem item } && !item.Requested && _active is not null)
        {
            RenderThumbnail(_active.Document, item);
        }
    }

    /// <summary>Miniatur nach einer Änderung der Markierungen neu zeichnen (sie enthält die Markierungen).</summary>
    private void RefreshThumbnail(OpenDocument open, int page)
    {
        if (open == _active && page >= 0 && page < open.Thumbnails.Count && open.Thumbnails[page].Requested)
        {
            RenderThumbnail(open.Document, open.Thumbnails[page]);
        }
    }

    private async void RenderThumbnail(PdfDocument document, ThumbnailItem item)
    {
        item.Requested = true;
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var info = document.GetPageInfo(item.Index);
        var scale = item.Width / info.WidthPt * dpi;
        var (w, h) = document.PagePixelSize(item.Index, scale);
        var image = await _renderer.RenderAsync(document, item.Index, scale, new PixelRect(0, 0, w, h), RenderPriority.Background, CancellationToken.None);
        if (image is not null)
        {
            item.Image = image;
        }
    }

    private void OnThumbnailSelected(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingSelection && Thumbnails.SelectedItem is ThumbnailItem item && _active is not null)
        {
            _active.Viewer.GoToPage(item.Index);
        }
    }

    private void BuildBookmarks()
    {
        var bookmarks = _active?.Document.GetBookmarks() ?? [];
        Bookmarks.ItemsSource = bookmarks;
        UpdateSidebarTab();
    }

    private void OnBookmarkSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is PdfBookmark { PageIndex: { } page } && _active is not null)
        {
            _active.Viewer.GoToPage(page);
        }
    }

    private void OnSidebarTab(object sender, RoutedEventArgs e) => UpdateSidebarTab();

    private void UpdateSidebarTab()
    {
        if (Thumbnails is null || Bookmarks is null || SearchPanel is null || ToolsPanel is null)
        {
            return;
        }
        var pages = PagesTab.IsChecked == true;
        var bookmarks = BookmarksTab.IsChecked == true;
        var hasBookmarks = Bookmarks.Items.Count > 0;
        Thumbnails.Visibility = pages ? Visibility.Visible : Visibility.Collapsed;
        Bookmarks.Visibility = bookmarks && hasBookmarks ? Visibility.Visible : Visibility.Collapsed;
        NoBookmarks.Visibility = bookmarks && !hasBookmarks ? Visibility.Visible : Visibility.Collapsed;
        SearchPanel.Visibility = SearchTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ToolsPanel.Visibility = ToolsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnToggleSidebar(object sender, RoutedEventArgs e)
    {
        var visible = Sidebar.Visibility == Visibility.Visible;
        Sidebar.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        SidebarSplitter.Visibility = Sidebar.Visibility;
        SidebarColumn.MinWidth = visible ? 0 : 140;
        SidebarColumn.Width = visible ? new GridLength(0) : new GridLength(App.Theme.Settings.DocumentSidebarWidth);
    }

    private void OnSplitterMoved(object sender, DragCompletedEventArgs e)
    {
        App.Theme.Settings.DocumentSidebarWidth = SidebarColumn.ActualWidth;
        App.Theme.SaveSettings();
    }

    private void SyncCurrentPage()
    {
        if (_active is null)
        {
            return;
        }
        var page = _active.Viewer.CurrentPage;
        _syncingSelection = true;
        Thumbnails.SelectedIndex = page;
        if (page >= 0 && Thumbnails.SelectedItem is { } item)
        {
            Thumbnails.ScrollIntoView(item);
        }
        _syncingSelection = false;
        RefreshPageInfo();
    }

    // ---------- Anzeige aktualisieren ----------

    private void RefreshUi()
    {
        var has = _active is not null;
        EmptyState.Visibility = has ? Visibility.Collapsed : Visibility.Visible;
        TabStripBorder.Visibility = _documents.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        var canModify = has && _active!.Document.CanModify;
        foreach (var b in new[] { SaveButton, SaveAsButton, SidebarButton, ZoomOutButton, ZoomInButton, FitWidthButton, FitPageButton, PrevButton, NextButton, PropertiesButton, ExtractButton, MarkupListButton, PrintButton })
        {
            b.IsEnabled = has;
        }
        foreach (var b in new[] { RotateLeftButton, RotateRightButton, InsertBlankButton, InsertFileButton, DeletePageButton })
        {
            b.IsEnabled = canModify;
        }
        ZoomBox.IsEnabled = PageBox.IsEnabled = has;
        if (!has && MarkupListVisible)
        {
            OnToggleMarkupList(this, new RoutedEventArgs());
        }
        if (has)
        {
            UpdateTab(_active!);
        }
        RefreshZoom();
        RefreshPageInfo();
        RefreshMarkupBar();
    }

    private void RefreshZoom()
    {
        var zoom = _active?.Viewer.Zoom;
        ZoomBox.Text = zoom is { } z ? $"{z * 100:0} %" : "";
        StatusZoom.Text = zoom is { } zz ? $"Zoom {zz * 100:0} %" : "";
    }

    private void RefreshPageInfo()
    {
        if (_active is null || _active.Viewer.PageCount == 0)
        {
            PageBox.Text = "";
            PageCountText.Text = "";
            RefreshViewHistory();
            StatusFormat.Text = "";
            StatusPath.Text = _active is null ? "" : "Das Dokument hat keine Seiten.";
            return;
        }
        var page = Math.Max(0, _active.Viewer.CurrentPage);
        var info = _active.Document.GetPageInfo(page);
        PageBox.Text = (page + 1).ToString(CultureInfo.CurrentCulture);
        PageCountText.Text = $"von {_active.Viewer.PageCount}";
        StatusFormat.Text = $"Seite {info.DisplayLabel}: {info.Format.Description}";
        StatusPath.Text = _active.Document.FilePath ?? "Noch nicht gespeichert";
        RefreshViewHistory();
        PrevButton.IsEnabled = page > 0;
        NextButton.IsEnabled = page < _active.Viewer.PageCount - 1;
        DeletePageButton.IsEnabled = _active.Document.CanModify && _active.Viewer.PageCount > 1;
    }

    private void RefreshRecent()
    {
        var recent = App.Theme.Settings.RecentDocuments.ToList();
        RecentList.ItemsSource = recent;
        RecentTitle.Visibility = recent.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowError(string message) =>
        MessageBox.Show(Owner, message, "EGLE360 PLAN", MessageBoxButton.OK, MessageBoxImage.Warning);

    // ---------- Befehle ----------

    private void OnOpen(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = PdfFilter, Multiselect = true };
        if (dialog.ShowDialog(Owner) == true)
        {
            OpenFiles(dialog.FileNames);
        }
    }

    private void OnOpenRecent(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string path })
        {
            OpenPath(path);
        }
    }

    /// <summary>Öffnet eine frisch erzeugte Kopie des Beispielplans aus dem Temp-Ordner, damit ohne eigene Pläne getestet werden kann.</summary>
    private void OnOpenSample(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = Path.Combine(Path.GetTempPath(), $"EGLE360-Beispielplan-{DateTime.Now:yyyyMMdd-HHmmss}.pdf");
            SamplePlan.Write(path, new WpfTextOutliner());
            OpenFiles([path]);
        }
        catch (Exception ex)
        {
            ShowError($"Der Beispielplan konnte nicht erzeugt werden: {ex.Message}");
        }
    }

    private void OnNew(object sender, RoutedEventArgs e)
    {
        var document = PdfDocument.CreateNew();
        var (w, h) = PaperFormat.IsoSizePoints("A4");
        document.InsertBlankPage(0, w, h);
        Activate(AddDocument(document, $"Unbenannt {++_untitledCounter}"));
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_active is not null)
        {
            Save(_active, saveAs: false);
        }
    }

    private void OnPrint(object sender, RoutedEventArgs e)
    {
        if (_active is not { } open)
        {
            return;
        }
        open.Viewer.CancelDrawing();
        try
        {
            PdfPrinter.Print(open.Document, open.Title, open.Viewer.CurrentPage);
        }
        catch (Exception ex) when (ex is PdfException or System.Printing.PrintSystemException or InvalidOperationException)
        {
            ShowError($"Drucken war nicht möglich: {ex.Message}");
        }
    }

    private void OnSaveAs(object sender, RoutedEventArgs e)
    {
        if (_active is not null)
        {
            Save(_active, saveAs: true);
        }
    }

    private void OnZoomIn(object sender, RoutedEventArgs e) => _active?.Viewer.ZoomIn();

    private void OnZoomOut(object sender, RoutedEventArgs e) => _active?.Viewer.ZoomOut();

    private void OnFitWidth(object sender, RoutedEventArgs e) => _active?.Viewer.FitWidth();

    private void OnFitPage(object sender, RoutedEventArgs e) => _active?.Viewer.FitPage();

    private void OnViewBack(object sender, RoutedEventArgs e) => _active?.Viewer.GoBack();

    private void OnViewForward(object sender, RoutedEventArgs e) => _active?.Viewer.GoForward();

    private void RefreshViewHistory()
    {
        BackButton.IsEnabled = _active?.Viewer.CanGoBack == true;
        ForwardButton.IsEnabled = _active?.Viewer.CanGoForward == true;
    }

    private void OnPrevPage(object sender, RoutedEventArgs e) => _active?.Viewer.GoToPage(_active.Viewer.CurrentPage - 1);

    private void OnNextPage(object sender, RoutedEventArgs e) => _active?.Viewer.GoToPage(_active.Viewer.CurrentPage + 1);

    private void OnZoomBoxKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _active is null)
        {
            return;
        }
        var text = ZoomBox.Text.Replace("%", "").Trim();
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var percent) && percent > 0)
        {
            _active.Viewer.SetZoom(percent / 100);
        }
        RefreshZoom();
        _active.Viewer.FocusView();
        e.Handled = true;
    }

    private void OnPageBoxKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _active is null)
        {
            return;
        }
        if (int.TryParse(PageBox.Text.Trim(), out var page))
        {
            _active.Viewer.GoToPage(page - 1);
        }
        RefreshPageInfo();
        _active.Viewer.FocusView();
        e.Handled = true;
    }

    /// <summary>Seite für Seitenbefehle: in der Miniaturliste gewählt, sonst die aktuelle Seite.</summary>
    private int TargetPage => _active is null ? -1 : Math.Max(0, _active.Viewer.CurrentPage);

    private void Run(Action<PdfDocument, int> action)
    {
        if (_active is null || TargetPage < 0)
        {
            return;
        }
        try
        {
            action(_active.Document, TargetPage);
        }
        catch (Exception ex) when (ex is PdfException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
    }

    private void OnRotateRight(object sender, RoutedEventArgs e) => Run((d, p) => d.RotatePage(p, 1));

    private void OnRotateLeft(object sender, RoutedEventArgs e) => Run((d, p) => d.RotatePage(p, -1));

    private void OnMoveUp(object sender, RoutedEventArgs e) => Run((d, p) =>
    {
        if (p > 0)
        {
            d.MovePage(p, p - 1);
            _active!.Viewer.GoToPage(p - 1);
        }
    });

    private void OnMoveDown(object sender, RoutedEventArgs e) => Run((d, p) =>
    {
        if (p < d.PageCount - 1)
        {
            d.MovePage(p, p + 1);
            _active!.Viewer.GoToPage(p + 1);
        }
    });

    private void OnDeletePage(object sender, RoutedEventArgs e) => Run((d, p) =>
    {
        if (d.PageCount <= 1)
        {
            ShowError("Die letzte Seite eines Dokuments kann nicht gelöscht werden.");
            return;
        }
        var label = d.GetPageInfo(p).DisplayLabel;
        if (MessageBox.Show(Owner, $"Seite {label} löschen? Beim Speichern wird die Datei neu geschrieben, die Seite ist danach nicht mehr enthalten.",
                "Seite löschen", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            d.DeletePage(p);
        }
    });

    private void OnInsertBlank(object sender, RoutedEventArgs e)
    {
        if (_active is null)
        {
            return;
        }
        // Aus der Werkzeugleiste: Format wählen. Aus dem Kontextmenü: Format der gewählten Seite.
        if (sender is not Button button)
        {
            Run((d, p) =>
            {
                var info = d.GetPageInfo(p);
                d.InsertBlankPage(p + 1, info.WidthPt, info.HeightPt);
            });
            return;
        }
        var menu = new ContextMenu { PlacementTarget = button, Placement = PlacementMode.Bottom };
        var same = new MenuItem { Header = "Wie aktuelle Seite" };
        same.Click += (_, _) => Run((d, p) =>
        {
            var info = d.GetPageInfo(p);
            d.InsertBlankPage(p + 1, info.WidthPt, info.HeightPt);
        });
        menu.Items.Add(same);
        menu.Items.Add(new Separator());
        foreach (var name in new[] { "A4", "A3", "A2", "A1", "A0" })
        {
            foreach (var landscape in new[] { false, true })
            {
                var item = new MenuItem { Header = $"{name} {(landscape ? "quer" : "hoch")}" };
                item.Click += (_, _) => Run((d, p) =>
                {
                    var (w, h) = PaperFormat.IsoSizePoints(name);
                    d.InsertBlankPage(p + 1, landscape ? h : w, landscape ? w : h);
                });
                menu.Items.Add(item);
            }
        }
        menu.IsOpen = true;
    }

    private void OnInsertFromFile(object sender, RoutedEventArgs e)
    {
        if (_active is null)
        {
            return;
        }
        var dialog = new OpenFileDialog { Filter = PdfFilter, Title = "Seiten einfügen aus" };
        if (dialog.ShowDialog(Owner) != true)
        {
            return;
        }
        using var source = TryOpen(dialog.FileName);
        if (source is null)
        {
            return;
        }
        var range = Dialogs.AskText(Owner, "Seiten einfügen",
            $"Welche Seiten aus „{Path.GetFileName(dialog.FileName)}“ ({source.PageCount} Seiten) sollen nach der aktuellen Seite eingefügt werden?",
            source.PageCount == 1 ? "1" : $"1-{source.PageCount}");
        if (range is null)
        {
            return;
        }
        try
        {
            var normalized = Egle360.Pdf.PageRange.Normalize(range, source.PageCount);
            Run((d, p) => d.ImportPages(source, normalized, p + 1));
        }
        catch (FormatException ex)
        {
            ShowError(ex.Message);
        }
    }

    private void OnExtract(object sender, RoutedEventArgs e)
    {
        if (_active is null)
        {
            return;
        }
        var document = _active.Document;
        var range = Dialogs.AskText(Owner, "Seiten als neues PDF speichern",
            $"Welche Seiten sollen in eine neue Datei? Das Dokument selbst bleibt unverändert. Es hat {document.PageCount} Seiten.",
            (TargetPage + 1).ToString(CultureInfo.CurrentCulture));
        if (range is null)
        {
            return;
        }
        try
        {
            var normalized = Egle360.Pdf.PageRange.Normalize(range, document.PageCount);
            var baseName = Path.GetFileNameWithoutExtension(document.FilePath ?? _active.Title);
            var dialog = new SaveFileDialog { Filter = PdfFilter, FileName = $"{baseName} Seiten {normalized}.pdf", AddExtension = true, DefaultExt = ".pdf" };
            if (dialog.ShowDialog(Owner) == true)
            {
                document.ExtractPages(normalized, dialog.FileName);
            }
        }
        catch (FormatException ex)
        {
            ShowError(ex.Message);
        }
        catch (Exception ex) when (ex is PdfException or IOException or UnauthorizedAccessException)
        {
            ShowError(ex.Message);
        }
    }

    /// <summary>Stapelverarbeitung mit den geöffneten, gespeicherten Dateien als Vorauswahl.</summary>
    private void OnBatch(object sender, RoutedEventArgs e) =>
        new BatchWindow(Owner, _documents.Select(d => d.Document.FilePath).OfType<string>()).Show();

    private void OnProperties(object sender, RoutedEventArgs e)
    {
        if (_active is not null)
        {
            Dialogs.ShowProperties(Owner, _active.Document, _active.Title);
        }
    }

    // ---------- Ziehen und Ablegen, Tastatur ----------

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = PdfFiles(e).Any() ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e) => OpenFiles(PdfFiles(e));

    private static IEnumerable<string> PdfFiles(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] files
            ? files.Where(f => string.Equals(Path.GetExtension(f), ".pdf", StringComparison.OrdinalIgnoreCase))
            : [];

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        var viewer = _active?.Viewer;
        if (HandleMarkupShortcut(e)) { }
        else if (KeyGestures.Ctrl(e, Key.F)) { ShowSearch(); }
        else if (e.Key == Key.System && Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Left) { viewer?.GoBack(); }
        else if (e.Key == Key.System && Keyboard.Modifiers == ModifierKeys.Alt && e.SystemKey == Key.Right) { viewer?.GoForward(); }
        else if (e.Key == Key.F3 && Keyboard.Modifiers is ModifierKeys.None or ModifierKeys.Shift) { StepSearch(Keyboard.Modifiers == ModifierKeys.Shift ? -1 : 1); }
        else if (KeyGestures.Ctrl(e, Key.O)) { OnOpen(this, e); }
        else if (KeyGestures.Ctrl(e, Key.P)) { OnPrint(this, e); }
        else if (KeyGestures.Ctrl(e, Key.N)) { OnNew(this, e); }
        else if (KeyGestures.Ctrl(e, Key.S)) { OnSave(this, e); }
        else if (KeyGestures.Ctrl(e, Key.S, shift: true)) { OnSaveAs(this, e); }
        else if (KeyGestures.Ctrl(e, Key.W) && _active is not null) { Close(_active); }
        else if (KeyGestures.Ctrl(e, Key.Tab) || KeyGestures.Ctrl(e, Key.Tab, shift: true)) { CycleTabs(Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) ? -1 : 1); }
        else if (KeyGestures.Ctrl(e, Key.OemPlus) || KeyGestures.Ctrl(e, Key.Add)) { viewer?.ZoomIn(); }
        else if (KeyGestures.Ctrl(e, Key.OemMinus) || KeyGestures.Ctrl(e, Key.Subtract)) { viewer?.ZoomOut(); }
        else if (KeyGestures.Ctrl(e, Key.D0) || KeyGestures.Ctrl(e, Key.NumPad0)) { viewer?.FitPage(); }
        else if (KeyGestures.Ctrl(e, Key.D1) || KeyGestures.Ctrl(e, Key.NumPad1)) { viewer?.SetZoom(1.0); }
        else if (KeyGestures.Ctrl(e, Key.D2) || KeyGestures.Ctrl(e, Key.NumPad2)) { viewer?.FitWidth(); }
        else { return; }
        e.Handled = true;
    }

    private void CycleTabs(int direction)
    {
        if (_documents.Count < 2 || _active is null)
        {
            return;
        }
        var i = (_documents.IndexOf(_active) + direction + _documents.Count) % _documents.Count;
        Activate(_documents[i]);
    }

    private sealed class OpenDocument(PdfDocument document, PdfViewer viewer, MarkupHistory history, string title)
    {
        public PdfDocument Document { get; } = document;

        public PdfViewer Viewer { get; } = viewer;

        public MarkupHistory History { get; } = history;

        public string Title { get; set; } = title;

        public RadioButton Tab { get; set; } = null!;

        public TextBlock TabTitle { get; set; } = null!;

        public List<ThumbnailItem> Thumbnails { get; set; } = [];
    }
}
