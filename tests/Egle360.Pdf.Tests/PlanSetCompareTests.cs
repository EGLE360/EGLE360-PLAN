using Egle360.Pdf.Compare;
using Egle360.Pdf.Plans;

namespace Egle360.Pdf.Tests;

/// <summary>Vergleich ganzer Plansätze: aktueller gegen vorherigen Index je Plan.</summary>
public class PlanSetCompareTests
{
    private static readonly Markups.ITextOutliner Text = new Markups.BlockTextOutliner();

    private static string Plan(string path, bool extraWall, double ox = 0)
    {
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 600, 400);
        using (var c = doc.EditContent(0))
        {
            c.Rectangle(50 + ox, 50, 500, 300, PdfColor.Black, null, 2);
            c.Line(50 + ox, 250, 200 + ox, 250, PdfColor.Black, 1);
            c.Line(120 + ox, 50, 120 + ox, 250, PdfColor.Black, 1);
            if (extraWall)
            {
                c.Line(300 + ox, 50, 300 + ox, 200, PdfColor.Black, 3);
            }
            c.Commit();
        }
        doc.SaveAs(path);
        return path;
    }

    [Fact]
    public void Plansatz_vergleichen_und_Bericht()
    {
        using var dir = new TempDir();
        var project = PlanProject.Create(Path.Combine(dir.Path, "Projekt"), "Test");
        var eg = project.AddPlan(Plan(dir.File("eg-a.pdf"), false), new PlanInfo("GR-EG", "A", "Grundriss EG"), "Prüfer");
        project.AddRevision(eg.Id, Plan(dir.File("eg-b.pdf"), true, ox: 6), "B", "Prüfer", "", takeOverMarkups: false, Text);
        var og = project.AddPlan(Plan(dir.File("og-a.pdf"), false), new PlanInfo("GR-OG", "A", "Grundriss OG"), "Prüfer");
        project.AddRevision(og.Id, Plan(dir.File("og-b.pdf"), false), "B", "Prüfer", "", takeOverMarkups: false, Text);
        project.AddPlan(Plan(dir.File("ug.pdf"), false), new PlanInfo("GR-UG", "A", "Grundriss UG"), "Prüfer");

        var plans = PlanSetComparer.Comparable(project.Register.Plans);
        Assert.Equal(["GR-EG", "GR-OG"], plans.Select(p => p.Number));
        var results = plans.Select(p => PlanSetComparer.Compare(project, p)).ToList();

        // EG: neue Wand trotz verschobenem Planinhalt genau ein Bereich; OG: unverändert.
        Assert.Equal(1, results[0].TotalChanges);
        Assert.Equal("1 Bereich auf 1 Seite", results[0].Summary);
        Assert.True(results[0].Pages[0].OffsetXmm > 1, results[0].Pages[0].OffsetXmm.ToString());
        Assert.Equal("Keine Unterschiede", results[1].Summary);
        Assert.Equal("Änderung gegenüber Index A", PlanSetComparer.CloudText(results[0]));

        var xlsx = PlanSetComparer.ToXlsx(results);
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(xlsx));
        Assert.NotNull(zip.GetEntry("xl/worksheets/sheet2.xml"));

        // Fehlende Datei ergibt einen Fehler im Ergebnis statt einer Ausnahme.
        File.Delete(project.FullPath(project.Register.Find(og.Id)!.Current));
        Assert.StartsWith("Fehler: ", PlanSetComparer.Compare(project, project.Register.Find(og.Id)!).Summary);
    }
}
