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

        // Planprojekt mit zwei Plänen, einer davon mit zwei Ständen (Testdaten aus dem Beispielplan).
        var planRoot = Path.Combine(Path.GetTempPath(), $"EGLE360-Planprojekt-{Environment.ProcessId}");
        var project = Egle360.Pdf.Plans.PlanProject.Create(planRoot, "Beispielprojekt Wohnanlage (Testdaten)");
        project.Import([sample], "Prüfer", takeOverMarkups: false, new WpfTextOutliner());
        var eg = project.Register.Plans[0];
        // Index B: dieselbe Zeichnung mit einer zusätzlichen Trennwand, damit der Vergleich etwas findet.
        var sampleB = Path.Combine(Path.GetTempPath(), $"EGLE360-Beispielplan-B-{Environment.ProcessId}.pdf");
        using (var b = Egle360.Pdf.PdfDocument.Open(sample))
        {
            var t = b.GetPageTransform(0);
            using (var c = b.EditContent(0))
            {
                var x = t.Box.Left + t.Box.Width * 0.45;
                c.Line(x, t.Box.Bottom + t.Box.Height * 0.3, x, t.Box.Bottom + t.Box.Height * 0.55, Egle360.Pdf.PdfColor.Black, 4);
                c.Commit();
            }
            b.SaveAs(sampleB);
        }
        project.AddRevision(eg.Id, sampleB, "B", "Prüfer", "Trennwand ergänzt", takeOverMarkups: false, new WpfTextOutliner());
        var og = project.AddPlan(sample, new Egle360.Pdf.Plans.PlanInfo("GR-OG-01", "A", "Grundriss Obergeschoss"), "Prüfer");
        project.Update(project.Register
            .Replace(project.Register.Find(eg.Id)! with { Discipline = "AR", Floor = "EG", Status = "Zur Ausführung", Author = "Planungsbüro (Testdaten)" })
            .Replace(og with { Discipline = "AR", Floor = "OG", Status = "Zur Prüfung" }));
        window.Plans.ShowProject(planRoot);

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

                    // Stempel, Bemaßung und Titelblock bei 100 %.
                    if (window.Documents.ActiveDocument?.GetMarkups(0).FirstOrDefault(m => m.Kind == Egle360.Pdf.Markups.MarkupKind.Stamp) is { } stamp)
                    {
                        viewer.SetZoom(1.0);
                        WaitForRender(window);
                        var r = Egle360.Pdf.Markups.MarkupGeometry.HandleRect(stamp);
                        if (theme == ThemePreference.Light)
                        {
                            // Unterschriftsbild unter dem Stempel (gezeichnete Testdaten).
                            var strokes = new System.Windows.Ink.StrokeCollection();
                            foreach (var line in new[]
                            {
                                new[] { (0.0, 30.0), (10, 5), (18, 35), (26, 8), (34, 30), (48, 20), (60, 28) },
                                new[] { (62.0, 30.0), (75, 12), (82, 30), (95, 18), (110, 26), (130, 22) },
                            })
                            {
                                var points = new System.Windows.Input.StylusPointCollection(line.Select(p => new System.Windows.Input.StylusPoint(p.Item1, p.Item2)));
                                strokes.Add(new System.Windows.Ink.Stroke(points, new System.Windows.Ink.DrawingAttributes
                                {
                                    Color = Color.FromRgb(20, 40, 110), Width = 2.6, Height = 2.6, FitToCurve = true,
                                }));
                            }
                            var image = Egle360.Desktop.Views.DocumentsView.RenderStrokes(strokes);
                            var signature = Egle360.Pdf.Markups.Markup.Create(Egle360.Pdf.Markups.MarkupKind.Image,
                                [new[] { new Egle360.Pdf.Markups.PdfPoint(r.Left, r.Bottom - 70), new Egle360.Pdf.Markups.PdfPoint(r.Left + 150, r.Bottom - 10) }],
                                Egle360.Pdf.Markups.MarkupStyle.DefaultFor(Egle360.Pdf.Markups.MarkupKind.Image), "Prüfer") with { Image = image };
                            window.Documents.TryAddMarkups(sample, 0, [signature], "Unterschrift");
                        }
                        viewer.CenterOn(0, new Egle360.Pdf.Markups.PdfPoint((r.Left + r.Right) / 2 + 100, (r.Bottom + r.Top) / 2 + 80));
                        WaitForDocuments(window);
                        Save(window, Path.Combine(outputDir, name + "-stempel.png"));
                    }
                    if (window.Documents.ActiveDocument?.GetMarkups(0).FirstOrDefault(m => m.Kind == Egle360.Pdf.Markups.MarkupKind.Dimension) is { } dimension)
                    {
                        viewer.SetZoom(1.0);
                        WaitForRender(window);
                        var (p0, p1) = (dimension.Points[0], dimension.Points[1]);
                        viewer.CenterOn(0, new Egle360.Pdf.Markups.PdfPoint((p0.X + p1.X) / 2, (p0.Y + p1.Y) / 2 + 40));
                        WaitForDocuments(window);
                        Save(window, Path.Combine(outputDir, name + "-bemassung.png"));
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

                    // Werkzeugbibliothek mit aktivem Werkzeug.
                    window.Documents.ShowToolsForScreenshot("Bodenfläche");
                    WaitForDocuments(window);
                    Save(window, Path.Combine(outputDir, name + "-werkzeuge.png"));
                    window.Documents.EndSearchForScreenshot();
                    viewer.FitWidth();
                    continue;
                }
                if (page == "plans")
                {
                    window.Plans.SelectFirstForScreenshot();
                    WaitForRender(window);
                    Save(window, Path.Combine(outputDir, name + ".png"));
                    var compare = window.Plans.OpenCompareForScreenshot();
                    if (compare is not null)
                    {
                        compare.Width = 1200;
                        compare.Height = 800;
                        WaitForRender(compare);
                        Save(compare, Path.Combine(outputDir, name + "-vergleich.png"));
                        compare.Close();
                    }
                    if (window.Plans.OpenPlanSetCompare() is { } set)
                    {
                        set.Width = 1100;
                        set.Height = 700;
                        WaitForRender(set);
                        var until = DateTime.UtcNow.AddSeconds(60);
                        while (set.Running is not { IsCompleted: true } && DateTime.UtcNow < until)
                        {
                            WaitForRender(set);
                            Thread.Sleep(50);
                        }
                        WaitForRender(set);
                        Save(set, Path.Combine(outputDir, name + "-plansatz.png"));
                        set.Close();
                    }
                    var batchFiles = project.Register.Plans.Select(p => project.FullPath(p.Current)).ToList();
                    var batch = new Egle360.Desktop.Views.BatchWindow(window, batchFiles, Path.Combine(planRoot, "Ausgabe")) { Width = 1100, Height = 760, ShowActivated = false };
                    batch.Show();
                    WaitForRender(batch);
                    Save(batch, Path.Combine(outputDir, name + "-stapel.png"));
                    batch.Close();
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
        // Die Unterschrift hat den temporären Beispielplan verändert; ohne Speichern käme beim Beenden eine Rückfrage.
        window.Documents.SaveAllForScreenshot();
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
