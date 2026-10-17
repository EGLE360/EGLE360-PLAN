using Egle360.Pdf.Batch;
using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Stapelverarbeitung: Ablauf, Protokoll, Fortsetzen, Abbruch und die Arbeitsschritte.</summary>
public class BatchTests
{
    private static readonly ITextOutliner Text = new BlockTextOutliner();

    private static string Blank(TempDir dir, string name, int pages = 1, int rotation = 0)
    {
        var path = dir.File(name);
        using var doc = PdfDocument.CreateNew();
        for (var i = 0; i < pages; i++)
        {
            doc.InsertBlankPage(i, 600, 400);
            if (rotation != 0)
            {
                doc.RotatePage(i, rotation / 90);
            }
        }
        doc.SaveAs(path);
        return path;
    }

    private static StampBatchAction Stamp(BatchPages pages = BatchPages.First) =>
        new(StampPreset.Defaults[0], "Prüfer", pages, PageCorner.TopRight, Text);

    [Fact]
    public async Task Stempel_in_Kopien_mit_Protokoll_und_Fortsetzen()
    {
        using var dir = new TempDir();
        var a = Blank(dir, "a.pdf", pages: 2);
        var b = Blank(dir, "b.pdf");
        var broken = dir.File("kaputt.pdf");
        File.WriteAllText(broken, "kein PDF");
        var missing = dir.File("fehlt.pdf");
        var output = dir.File("ausgabe");
        var seen = new List<BatchProgress>();

        var report = await BatchRunner.RunAsync([a, b, broken, missing], Stamp(BatchPages.All), new BatchOptions(output), new SyncProgress(seen.Add), default)
            ;

        Assert.Equal([BatchStatus.Done, BatchStatus.Done, BatchStatus.Failed, BatchStatus.Failed], report.Entries.Select(e => e.Status));
        Assert.Equal("2 bearbeitet, 2 Fehler", report.Summary);
        Assert.Contains("Stempel auf 2 Seiten gesetzt.", report.ToText());
        Assert.Equal(4, seen[^1].Done);
        // Originale bleiben unverändert, die Kopien tragen die Stempel.
        using (var original = PdfDocument.Open(a))
        {
            Assert.Empty(original.GetMarkups(0));
        }
        using (var copy = PdfDocument.Open(Path.Combine(output, "a.pdf")))
        {
            Assert.Equal(MarkupKind.Stamp, Assert.Single(copy.GetMarkups(1)).Kind);
        }

        // Zweiter Lauf: bereits erledigte Dateien werden übersprungen.
        var again = await BatchRunner.RunAsync([a, b], Stamp(), new BatchOptions(output), null, default);
        Assert.All(again.Entries, e => Assert.Equal(BatchStatus.Skipped, e.Status));

        // Zielordner gleich Quellordner wird abgelehnt.
        var same = await BatchRunner.RunAsync([a], Stamp(), new BatchOptions(dir.Path), null, default);
        Assert.Equal(BatchStatus.Failed, same.Entries[0].Status);
    }

    [Fact]
    public async Task Abbruch_markiert_die_restlichen_Dateien()
    {
        using var dir = new TempDir();
        var files = Enumerable.Range(0, 3).Select(i => Blank(dir, $"p{i}.pdf")).ToList();
        using var cancel = new CancellationTokenSource();
        var report = await BatchRunner.RunAsync(files, Stamp(), new BatchOptions(null), null, cancel.Token,
            between: () => { cancel.Cancel(); return Task.CompletedTask; });
        Assert.Equal([BatchStatus.Done, BatchStatus.Cancelled, BatchStatus.Cancelled], report.Entries.Select(e => e.Status));
        // Ohne Zielordner wird das Original bearbeitet.
        using var doc = PdfDocument.Open(files[0]);
        Assert.Single(doc.GetMarkups(0));
    }

    [Fact]
    public void Stempel_liegt_auf_dem_Bildschirm_oben_rechts_auch_bei_gedrehter_Seite()
    {
        using var dir = new TempDir();
        using var doc = PdfDocument.Open(Blank(dir, "quer.pdf", rotation: 90));
        var t = doc.GetPageTransform(0);
        var m = Stamp().Place(t);
        var r = PdfRect.FromPoints(m.Points[0], m.Points[1]);
        var (u0, v0) = t.ToDisplay(new PdfPoint(r.Left, r.Bottom));
        var (u1, v1) = t.ToDisplay(new PdfPoint(r.Right, r.Top));
        Assert.Equal(t.DisplayWidth - StampBatchAction.Margin, Math.Max(u0, u1), 6);
        Assert.Equal(StampBatchAction.Margin, Math.Min(v0, v1), 6);
        Assert.Equal(90, m.TextRotation);
    }

    [Fact]
    public async Task Massstab_fuer_alle_Seiten_nur_wo_noetig()
    {
        using var dir = new TempDir();
        var path = Blank(dir, "m.pdf", pages: 3);
        var scale = MeasureScale.FromRatio(100);
        var action = new ScaleBatchAction(scale, BatchPages.All);
        var first = await BatchRunner.RunAsync([path], action, new BatchOptions(null), null, default);
        Assert.Equal("Maßstab 1:100 für 3 Seiten gesetzt.", first.Entries[0].Message);
        var second = await BatchRunner.RunAsync([path], action, new BatchOptions(null), null, default);
        Assert.Equal(BatchStatus.Unchanged, second.Entries[0].Status);
    }

    /// <summary>Progress ohne Synchronisationskontext, damit die Meldungen sofort ankommen.</summary>
    private sealed class SyncProgress(Action<BatchProgress> report) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => report(value);
    }

    [Fact]
    public void Seiten_drehen()
    {
        using var dir = new TempDir();
        using var doc = PdfDocument.Open(Blank(dir, "a.pdf", pages: 2));
        Assert.Equal("2 Seiten gedreht.", new RotateBatchAction(1, BatchPages.All).Apply(doc));
        Assert.Equal([90, 90], [doc.GetPageRotation(0), doc.GetPageRotation(1)]);
        Assert.Equal("1 Seite gedreht.", new RotateBatchAction(-1, BatchPages.First).Apply(doc));
        Assert.Equal([0, 90], [doc.GetPageRotation(0), doc.GetPageRotation(1)]);
        Assert.Null(new RotateBatchAction(4, BatchPages.All).Apply(doc));
    }

    [Fact]
    public void Markierungen_einbrennen_behaelt_den_Massstab()
    {
        using var dir = new TempDir();
        using var doc = PdfDocument.Open(Blank(dir, "a.pdf"));
        var scale = MeasureScale.FromRatio(100);
        doc.SetPageScale(0, scale);
        var box = Markup.Create(MarkupKind.Rectangle, [[new PdfPoint(100, 100), new PdfPoint(300, 250)]],
            MarkupStyle.DefaultFor(MarkupKind.Rectangle) with { Stroke = PdfColor.Black, LineWidth = 4 }, "Prüfer");
        doc.SetMarkups(0, [box], Text);
        using (var empty = PdfDocument.Open(Blank(dir, "leer.pdf")))
        {
            Assert.Null(new FlattenBatchAction().Apply(empty));
        }

        Assert.Equal("Markierungen auf 1 Seite eingebrannt.", new FlattenBatchAction().Apply(doc));
        Assert.Empty(doc.GetMarkups(0));
        Assert.Equal(scale.MetersPerPoint, doc.GetPageScale(0)!.MetersPerPoint, 9);
        // Das Rechteck ist jetzt Seiteninhalt: auch ohne Anmerkungen gerendert dunkel.
        var (w, h) = doc.PagePixelSize(0, 1);
        var pixels = new byte[w * h * 4];
        doc.Render(0, 1, new PixelRect(0, 0, w, h), pixels, w * 4, annotations: false);
        var i = ((400 - 100) * w + 200) * 4;
        Assert.True(pixels[i] < 128, $"Pixel {pixels[i]}");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    public void Kopf_und_Fusszeile_mit_Platzhaltern(int rotation)
    {
        using var dir = new TempDir();
        using var doc = PdfDocument.Open(Blank(dir, "Grundriss EG.pdf", pages: 2, rotation: rotation));
        var action = new HeaderFooterBatchAction("{Datei} Seite {Seite} von {Seiten}, {Datum}", PageEdgePosition.BottomRight, 9, BatchPages.All,
            new DateTime(2026, 10, 17));
        Assert.Equal("Text auf 2 Seiten gesetzt.", action.Apply(doc));
        Assert.Contains("Grundriss EG Seite 2 von 2, 17.10.2026", doc.GetPageText(1));

        // Der Text steht unten rechts, wie die Seite angezeigt wird, und aufrecht.
        var t = doc.GetPageTransform(0);
        var (w, h) = doc.PagePixelSize(0, 1);
        var pixels = new byte[w * h * 4];
        doc.Render(0, 1, new PixelRect(0, 0, w, h), pixels, w * 4, annotations: false);
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (pixels[(y * w + x) * 4] < 128)
                {
                    (minX, minY, maxX, maxY) = (Math.Min(minX, x), Math.Min(minY, y), Math.Max(maxX, x), Math.Max(maxY, y));
                }
            }
        }
        Assert.InRange(maxX, w - HeaderFooterBatchAction.Margin - 3, w - HeaderFooterBatchAction.Margin + 1);
        Assert.InRange(maxY, h - HeaderFooterBatchAction.Margin - 1, h - HeaderFooterBatchAction.Margin + 4);
        Assert.True(maxX - minX > 4 * (maxY - minY), $"{minX},{minY} bis {maxX},{maxY}");
        Assert.Equal(t.DisplayWidth, w, 0);
    }
}
