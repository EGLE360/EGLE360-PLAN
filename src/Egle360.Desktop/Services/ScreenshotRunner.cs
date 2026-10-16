using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Egle360.Design;

namespace Egle360.Desktop.Services;

/// <summary>
/// Startet die Oberfläche ohne Benutzer, rendert jede Seite in beiden Farbmodi als PNG und beendet sich.
/// Aufruf: EGLE360.PLAN.exe --screenshots &lt;Ordner&gt;. Wird in der CI als Rauchtest genutzt.
/// </summary>
internal static class ScreenshotRunner
{
    public static int Run(MainWindow window, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        window.ShowActivated = false;
        window.Width = 1440;
        window.Height = 900;
        window.Show();

        // Beispielplan für die Dokumentansicht (frei erfundene Testdaten).
        var sample = Path.Combine(Path.GetTempPath(), $"EGLE360-Beispielplan-{Environment.ProcessId}.pdf");
        SamplePlan.Write(sample, new WpfTextOutliner());
        window.OpenDocuments([sample]);

        foreach (var theme in new[] { ThemePreference.Light, ThemePreference.Dark })
        {
            App.Theme.SetPreference(theme);
            foreach (var page in window.PageIds)
            {
                window.ShowPage(page);
                WaitForRender(window);
                var name = $"{page}-{theme.ToString().ToLowerInvariant()}";
                if (page == "documents" && window.Documents.ActiveViewer is { } viewer)
                {
                    viewer.FitWidth();
                    WaitForDocuments(window);
                    Save(window, Path.Combine(outputDir, name + ".png"));

                    // Hoher Zoom: Detaildarstellung des sichtbaren Ausschnitts.
                    while (viewer.Zoom < 4.0)
                    {
                        viewer.ZoomIn(); // um die Fenstermitte, damit Planinhalt im Bild ist
                    }
                    WaitForDocuments(window);
                    Save(window, Path.Combine(outputDir, name + "-zoom.png"));

                    // Markierungen bei 100 %, eine davon ausgewählt (Griffe sichtbar).
                    if (window.Documents.ActiveDocument?.GetMarkups(0) is { Count: > 0 } markups)
                    {
                        var cloud = markups[0];
                        viewer.SetZoom(1.0);
                        WaitForRender(window);
                        var r = Egle360.Pdf.Markups.MarkupGeometry.HandleRect(cloud);
                        viewer.CenterOn(0, new Egle360.Pdf.Markups.PdfPoint(r.Right + 120, (r.Bottom + r.Top) / 2));
                        viewer.Select(0, cloud.Id);
                        WaitForDocuments(window);
                        Save(window, Path.Combine(outputDir, name + "-markierungen.png"));
                        viewer.ClearSelection();
                    }
                    // Mengenermittlung: Fläche der Küche ausgewählt, Wert in der Statuszeile.
                    if (window.Documents.ActiveDocument?.GetMarkups(0).FirstOrDefault(m => m.Kind == Egle360.Pdf.Markups.MarkupKind.Area) is { } area)
                    {
                        window.Documents.SetMarkupListVisible(true);
                        viewer.SetZoom(0.35);
                        WaitForRender(window);
                        var r = Egle360.Pdf.Markups.MarkupGeometry.HandleRect(area);
                        viewer.CenterOn(0, new Egle360.Pdf.Markups.PdfPoint((r.Left + r.Right) / 2, (r.Bottom + r.Top) / 2));
                        viewer.Select(0, area.Id);
                        WaitForDocuments(window);
                        Save(window, Path.Combine(outputDir, name + "-messen.png"));
                        viewer.ClearSelection();
                        window.Documents.SetMarkupListVisible(false);
                    }

                    // Druck ohne Drucker prüfen: beide Seiten auf A4 hoch als XPS ausgeben.
                    if (theme == ThemePreference.Light && window.Documents.ActiveDocument is { } printDoc)
                    {
                        var xpsPath = Path.Combine(outputDir, "druck-test.xps");
                        using (var xps = new System.Windows.Xps.Packaging.XpsDocument(xpsPath, FileAccess.ReadWrite))
                        {
                            System.Windows.Xps.Packaging.XpsDocument.CreateXpsDocumentWriter(xps)
                                .Write(PdfPrinter.CreatePaginator(printDoc, [0, 1], new Size(793.7, 1122.5)));
                        }
                        if (new FileInfo(xpsPath).Length < 10_000)
                        {
                            throw new InvalidOperationException("Die Druckausgabe ist leer.");
                        }
                    }

                    // Textsuche mit Trefferliste in der Seitenleiste.
                    var search = window.Documents.SearchAsync("Zimmer");
                    while (!search.IsCompleted)
                    {
                        WaitForRender(window);
                        Thread.Sleep(20);
                    }
                    WaitForDocuments(window);
                    Save(window, Path.Combine(outputDir, name + "-suche.png"));
                    window.Documents.EndSearchForScreenshot();
                    viewer.FitWidth();
                    continue;
                }
                Save(window, Path.Combine(outputDir, name + ".png"));

                // Längere Seiten zusätzlich am Ende aufnehmen.
                if (FindScrollViewer(window.PageContent) is { ScrollableHeight: > 0 } scroll)
                {
                    scroll.ScrollToEnd();
                    WaitForRender(window);
                    Save(window, Path.Combine(outputDir, name + "-ende.png"));
                }
            }
        }
        return 0;
    }

    private static ScrollViewer? FindScrollViewer(DependencyObject? node)
    {
        if (node is null)
        {
            return null;
        }
        if (node is ScrollViewer sv)
        {
            return sv;
        }
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(node); i++)
        {
            if (FindScrollViewer(VisualTreeHelper.GetChild(node, i)) is { } found)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>Wartet, bis alle Seiten und Miniaturen gerendert sind (höchstens 20 Sekunden).</summary>
    private static void WaitForDocuments(MainWindow window)
    {
        var until = DateTime.UtcNow.AddSeconds(20);
        do
        {
            WaitForRender(window);
            Thread.Sleep(50);
            WaitForRender(window);
        }
        while (window.Documents.IsBusy && DateTime.UtcNow < until);
        if (window.Documents.IsBusy)
        {
            throw new TimeoutException("Die Dokumentansicht ist nach 20 Sekunden nicht fertig gerendert.");
        }
    }

    private static void WaitForRender(Window window)
    {
        window.UpdateLayout();
        window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Save(Window window, string path)
    {
        var root = (FrameworkElement)window.Content;
        var bitmap = new RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
