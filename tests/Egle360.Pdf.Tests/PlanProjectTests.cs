using Egle360.Pdf.Markups;
using Egle360.Pdf.Plans;

namespace Egle360.Pdf.Tests;

/// <summary>Planverwaltung: Planliste, Erkennung von Plannummer und Index, Planstände und Übernahme der Markierungen.</summary>
public class PlanProjectTests
{
    private static string MakePlan(string dir, string fileName, string titleBlock)
    {
        var path = Path.Combine(dir, fileName);
        using var doc = PdfDocument.CreateNew();
        doc.InsertBlankPage(0, 842, 595);
        using (var c = doc.EditContent(0))
        {
            c.Text(600, 60, titleBlock, 12, PdfColor.Black);
            c.Commit();
        }
        doc.SaveAs(path);
        return path;
    }

    [Theory]
    [InlineData("AR-101_Grundriss EG_Index B.pdf", "AR-101", "B", "Grundriss EG")]
    [InlineData("TW_203 Bewehrung Decke.pdf", "TW_203", "", "Bewehrung Decke")]
    [InlineData("GR-EG-01 Grundriss rev C.pdf", "GR-EG-01", "C", "Grundriss")]
    [InlineData("Lageplan.pdf", "", "", "Lageplan")]
    [InlineData("0815-02_Schnitt_idx03.pdf", "0815-02", "03", "Schnitt")]
    public void Plannummer_und_Index_aus_Dateinamen(string file, string number, string index, string title)
    {
        var info = PlanInfoReader.FromFileName(file);
        Assert.Equal(number, info.Number);
        Assert.Equal(index, info.Index);
        Assert.Equal(title, info.Title);
    }

    [Fact]
    public void Plankopf_hat_Vorrang_vor_dem_Dateinamen()
    {
        var info = PlanInfoReader.Read("scan_0001.pdf", "Beispielprojekt\nGrundriss Erdgeschoss\nPlannummer GR-EG-01\nIndex a\nMaßstab 1:50");
        Assert.Equal("GR-EG-01", info.Number);
        Assert.Equal("A", info.Index);
        Assert.Equal("TW-12/3", PlanInfoReader.Read("x.pdf", "Plan-Nr.: TW-12/3").Number);
    }

    [Fact]
    public void Planliste_uebersteht_Speichern_und_lehnt_fremde_Pfade_ab()
    {
        var plan = new PlanEntry("p1", "AR-101", "Grundriss", [new PlanRevision("A", "plans/AR-101/a.pdf", DateTimeOffset.Now, "Elias", new DateOnly(2026, 10, 1), "Erststand")],
            Discipline: "AR", Status: "Freigegeben");
        var reg = new PlanRegister("Wohnanlage", [plan]);
        var read = PlanRegister.FromJson(reg.ToJson());
        Assert.Equal("Wohnanlage", read.Name);
        var p = Assert.Single(read.Plans);
        Assert.Equal(plan.Number, p.Number);
        Assert.Equal(plan.Revisions, p.Revisions);
        Assert.Equal("AR", p.Discipline);

        var evil = reg.ToJson().Replace("plans/AR-101/a.pdf", "../../Windows/x.pdf");
        Assert.Empty(PlanRegister.FromJson(evil).Plans);
        Assert.False(PlanRegister.IsSafeRelative("C:/x.pdf"));
        Assert.Throws<FormatException>(() => PlanRegister.FromJson("{}"));

        Assert.Same(read.Plans[0], read.FindByNumber("ar 101"));
        Assert.Single(read.Filter("grund"));
        Assert.Empty(read.Filter("", "TW"));
    }

    [Fact]
    public void Neuer_Index_wird_eingetragen_und_uebernimmt_Markierungen()
    {
        using var dir = new TempDir();
        var inbox = Directory.CreateDirectory(Path.Combine(dir.Path, "Eingang")).FullName;
        var first = MakePlan(inbox, "plan1.pdf", "Plannummer AR-101  Index A");
        var outliner = new BlockTextOutliner();
        using (var doc = PdfDocument.Open(first))
        {
            doc.SetPageScale(0, MeasureScale.FromRatio(50));
            doc.SetMarkups(0, [Markup.Create(MarkupKind.Cloud, [[new(100, 100), new(200, 100), new(200, 200), new(100, 200)]],
                MarkupStyle.DefaultFor(MarkupKind.Cloud), "Elias", text: "Tür fehlt")], outliner);
            doc.Save();
        }
        var second = MakePlan(inbox, "plan1 neu.pdf", "Plannummer AR-101  Index B");
        var other = MakePlan(inbox, "TW-200 Decke.pdf", "Tragwerk");
        var broken = Path.Combine(inbox, "kaputt.pdf");
        File.WriteAllText(broken, "kein PDF");

        var root = Path.Combine(dir.Path, "Projekt");
        var project = PlanProject.Create(root, "Wohnanlage");
        Assert.Throws<IOException>(() => PlanProject.Create(root, "nochmal"));

        var results = project.Import([first, other, broken, second, first], "Elias", takeOverMarkups: true, outliner);
        Assert.Equal([PlanImportAction.NewPlan, PlanImportAction.NewPlan, PlanImportAction.Failed, PlanImportAction.NewRevision, PlanImportAction.Duplicate],
            results.Select(r => r.Action));
        Assert.Equal(1, results[3].MarkupsTakenOver);

        var reopened = PlanProject.Open(root);
        Assert.Equal(2, reopened.Register.Plans.Count);
        var ar = reopened.Register.FindByNumber("AR-101")!;
        Assert.Equal(["A", "B"], ar.Revisions.Select(r => r.Index));
        Assert.Equal("B", ar.Index);
        Assert.Equal("TW-200", reopened.Register.FindByNumber("TW-200")!.Number);
        Assert.Equal("Decke", reopened.Register.FindByNumber("TW-200")!.Title);

        // Beide Stände liegen als eigene Dateien im Projekt; die Originale sind unverändert.
        Assert.All(ar.Revisions, r => Assert.True(File.Exists(reopened.FullPath(r)), r.File));
        Assert.StartsWith("Pläne/AR-101/", ar.Current.File);
        using (var current = PdfDocument.Open(reopened.FullPath(ar.Current)))
        {
            var m = Assert.Single(current.GetMarkups(0));
            Assert.Equal("Tür fehlt", m.Text);
            Assert.Equal("1:50", current.GetPageScale(0)?.Label);
        }
        using (var original = PdfDocument.Open(second))
        {
            Assert.Empty(original.GetMarkups(0));
        }
    }

    [Fact]
    public void Ordnernamen_ohne_verbotene_Zeichen()
    {
        Assert.Equal("TW-12_3", PlanProject.SafeName("TW-12/3"));
        Assert.Equal("Ohne Nummer", PlanProject.SafeName(" .. "));
    }
}
