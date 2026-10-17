using Egle360.Pdf.Export;
using Egle360.Pdf.Markups;
using Egle360.Pdf.Plans;

namespace Egle360.Pdf.Compare;

/// <summary>Ergebnis einer Seite: geänderte Bereiche im neuen Stand und ausgeglichener Versatz in Millimetern.</summary>
public sealed record PageChangeSummary(int Page, IReadOnlyList<PdfRect> Changes, double OffsetXmm, double OffsetYmm);

/// <summary>Vergleich des aktuellen mit dem vorherigen Stand eines Plans.</summary>
/// <param name="Error">Fehlermeldung, wenn eine der Dateien nicht gelesen werden konnte; dann ist Pages leer.</param>
public sealed record PlanComparisonResult(PlanEntry Plan, PlanRevision Old, PlanRevision New, int PagesOld, int PagesNew,
    IReadOnlyList<PageChangeSummary> Pages, string? Error = null)
{
    public int TotalChanges => Pages.Sum(p => p.Changes.Count);

    public int ChangedPages => Pages.Count(p => p.Changes.Count > 0);

    /// <summary>Kurztext für Listen, z. B. „3 Bereiche auf 2 Seiten“.</summary>
    public string Summary => Error is not null
        ? $"Fehler: {Error}"
        : TotalChanges == 0
            ? PagesOld == PagesNew ? "Keine Unterschiede" : $"Keine Unterschiede auf den gemeinsamen Seiten, Seitenzahl {PagesOld} statt {PagesNew}"
            : $"{TotalChanges} {(TotalChanges == 1 ? "Bereich" : "Bereiche")} auf {ChangedPages} {(ChangedPages == 1 ? "Seite" : "Seiten")}"
                + (PagesOld != PagesNew ? $", Seitenzahl {PagesOld} statt {PagesNew}" : "");
}

/// <summary>
/// Vergleicht ganze Plansätze: je Plan den aktuellen Stand mit dem vorherigen Index, Seite für Seite mit automatischer
/// Ausrichtung. Pläne mit nur einem Stand werden übersprungen.
/// </summary>
public static class PlanSetComparer
{
    /// <summary>Pläne, die sich vergleichen lassen (mindestens zwei Stände).</summary>
    public static IReadOnlyList<PlanEntry> Comparable(IEnumerable<PlanEntry> plans) => [.. plans.Where(p => p.Revisions.Count >= 2)];

    public static PlanComparisonResult Compare(PlanProject project, PlanEntry plan, CancellationToken cancel = default)
    {
        var old = plan.Revisions[^2];
        var current = plan.Current;
        try
        {
            using var a = PdfDocument.Open(project.FullPath(old));
            using var b = PdfDocument.Open(project.FullPath(current));
            var pages = new List<PageChangeSummary>();
            for (var p = 0; p < Math.Min(a.PageCount, b.PageCount); p++)
            {
                cancel.ThrowIfCancellationRequested();
                var r = PageComparer.Compare(a, p, b, p, PageComparer.ScaleFor(a, p, b, p), align: true);
                var (mx, my) = r.OffsetMillimeters;
                pages.Add(new PageChangeSummary(p, r.Changes, mx, my));
            }
            return new PlanComparisonResult(plan, old, current, a.PageCount, b.PageCount, pages);
        }
        catch (Exception ex) when (ex is PdfException or IOException or UnauthorizedAccessException)
        {
            return new PlanComparisonResult(plan, old, current, 0, 0, [], ex.Message);
        }
    }

    /// <summary>Text der Änderungswolken, z. B. „Änderung gegenüber Index A“.</summary>
    public static string CloudText(PlanComparisonResult r) => $"Änderung gegenüber {(r.Old.Index.Length == 0 ? "dem vorherigen Stand" : $"Index {r.Old.Index}")}";

    /// <summary>Änderungsbericht: ein Blatt mit einer Zeile je Plan, eines mit jedem geänderten Bereich.</summary>
    public static byte[] ToXlsx(IReadOnlyList<PlanComparisonResult> results)
    {
        var plans = new XlsxSheet("Änderungen je Plan", [16, 40, 10, 10, 12, 14, 50]);
        plans.Rows.Add([.. new[] { "Plannummer", "Bezeichnung", "Alter Index", "Neuer Index", "Seiten", "Bereiche", "Ergebnis" }.Select(XlsxCell.Header)]);
        foreach (var r in results)
        {
            plans.Rows.Add([
                XlsxCell.Text(r.Plan.Number), XlsxCell.Text(r.Plan.Title), XlsxCell.Text(r.Old.Index), XlsxCell.Text(r.New.Index),
                XlsxCell.Integer(r.PagesNew), XlsxCell.Integer(r.TotalChanges), XlsxCell.Text(r.Summary),
            ]);
        }
        var regions = new XlsxSheet("Bereiche", [16, 10, 8, 12, 12, 12, 12, 14, 14]);
        regions.Rows.Add([.. new[] { "Plannummer", "Index", "Seite", "Links (mm)", "Unten (mm)", "Breite (mm)", "Höhe (mm)", "Versatz x (mm)", "Versatz y (mm)" }
            .Select(XlsxCell.Header)]);
        const double mm = 25.4 / 72;
        foreach (var r in results)
        {
            foreach (var p in r.Pages)
            {
                foreach (var c in p.Changes)
                {
                    regions.Rows.Add([
                        XlsxCell.Text(r.Plan.Number), XlsxCell.Text(r.New.Index), XlsxCell.Integer(p.Page + 1),
                        XlsxCell.Number(Math.Round(c.Left * mm, 1)), XlsxCell.Number(Math.Round(c.Bottom * mm, 1)),
                        XlsxCell.Number(Math.Round(c.Width * mm, 1)), XlsxCell.Number(Math.Round(c.Height * mm, 1)),
                        XlsxCell.Number(Math.Round(p.OffsetXmm, 1)), XlsxCell.Number(Math.Round(p.OffsetYmm, 1)),
                    ]);
                }
            }
        }
        return XlsxWriter.Write([plans, regions]);
    }
}
