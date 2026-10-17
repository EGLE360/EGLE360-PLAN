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
    public void Stempel_in_Kopien_mit_Protokoll_und_Fortsetzen()
    {
        using var dir = new TempDir();
        var a = Blank(dir, "a.pdf", pages: 2);
        var b = Blank(dir, "b.pdf");
        var broken = dir.File("kaputt.pdf");
        File.WriteAllText(broken, "kein PDF");
        var missing = dir.File("fehlt.pdf");
        var output = dir.File("ausgabe");
        var seen = new List<BatchProgress>();

        var report = BatchRunner.RunAsync([a, b, broken, missing], Stamp(BatchPages.All), new BatchOptions(output), new SyncProgress(seen.Add), default)
            .GetAwaiter().GetResult();

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
        var again = BatchRunner.RunAsync([a, b], Stamp(), new BatchOptions(output), null, default).GetAwaiter().GetResult();
        Assert.All(again.Entries, e => Assert.Equal(BatchStatus.Skipped, e.Status));

        // Zielordner gleich Quellordner wird abgelehnt.
        var same = BatchRunner.RunAsync([a], Stamp(), new BatchOptions(dir.Path), null, default).GetAwaiter().GetResult();
        Assert.Equal(BatchStatus.Failed, same.Entries[0].Status);
    }

    [Fact]
    public void Abbruch_markiert_die_restlichen_Dateien()
    {
        using var dir = new TempDir();
        var files = Enumerable.Range(0, 3).Select(i => Blank(dir, $"p{i}.pdf")).ToList();
        using var cancel = new CancellationTokenSource();
        var report = BatchRunner.RunAsync(files, Stamp(), new BatchOptions(null), null, cancel.Token,
            between: () => { cancel.Cancel(); return Task.CompletedTask; }).GetAwaiter().GetResult();
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
    public void Massstab_fuer_alle_Seiten_nur_wo_noetig()
    {
        using var dir = new TempDir();
        var path = Blank(dir, "m.pdf", pages: 3);
        var scale = MeasureScale.FromRatio(100);
        var action = new ScaleBatchAction(scale, BatchPages.All);
        var first = BatchRunner.RunAsync([path], action, new BatchOptions(null), null, default).GetAwaiter().GetResult();
        Assert.Equal("Maßstab 1:100 für 3 Seiten gesetzt.", first.Entries[0].Message);
        var second = BatchRunner.RunAsync([path], action, new BatchOptions(null), null, default).GetAwaiter().GetResult();
        Assert.Equal(BatchStatus.Unchanged, second.Entries[0].Status);
    }

    /// <summary>Progress ohne Synchronisationskontext, damit die Meldungen sofort ankommen.</summary>
    private sealed class SyncProgress(Action<BatchProgress> report) : IProgress<BatchProgress>
    {
        public void Report(BatchProgress value) => report(value);
    }
}
