using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Tests;

/// <summary>Werkzeugbibliothek: Werkzeuge speichern, anwenden, suchen und austauschen.</summary>
public class ToolLibraryTests
{
    private static Markup Area()
    {
        var m = Markup.Create(MarkupKind.Area, [[new(0, 0), new(100, 0), new(100, 100), new(0, 100)]],
            MarkupStyle.DefaultFor(MarkupKind.Area) with { Stroke = new PdfColor(10, 20, 30), Fill = new PdfColor(200, 210, 220) }, "Prüfer", text: "Estrich");
        return m with { Scale = MeasureScale.FromRatio(50), Depth = 0.07, UnitPrice = 24.5 };
    }

    [Fact]
    public void Werkzeug_aus_Markierung_uebertraegt_Darstellung_Hoehe_und_Preis()
    {
        var tool = ToolPreset.FromMarkup(Area(), "Estrich 7 cm");
        Assert.Equal(MarkupKind.Area, tool.Kind);
        Assert.Equal(0.07, tool.Depth);
        Assert.Equal(24.5, tool.UnitPrice);
        Assert.Equal("Estrich", tool.Text);
        Assert.Equal("Fläche, Tiefe 0,07 m, 24,50 €", tool.Summary);

        var drawn = Markup.Create(MarkupKind.Area, [[new(0, 0), new(50, 0), new(50, 50)]], MarkupStyle.DefaultFor(MarkupKind.Area), "Prüfer")
            .WithScale(MeasureScale.FromRatio(100));
        var applied = tool.ApplyTo(drawn);
        Assert.Equal(tool.Style, applied.Style);
        Assert.Equal(0.07, applied.Depth);
        Assert.Equal(24.5, applied.UnitPrice);
        Assert.Equal("Estrich", applied.Text);
        Assert.Equal(drawn.Scale, applied.Scale);

        // Andere Art: nichts übertragen.
        var line = Markup.Create(MarkupKind.Line, [[new(0, 0), new(1, 1)]], MarkupStyle.DefaultFor(MarkupKind.Line), "Prüfer");
        Assert.Same(line, tool.ApplyTo(line));

        // Höhe und Preis nur bei Arten, die sie kennen.
        var rect = Markup.Create(MarkupKind.Rectangle, [[new(0, 0), new(1, 1)]], MarkupStyle.DefaultFor(MarkupKind.Rectangle), "Prüfer") with { Depth = 3, UnitPrice = 5 };
        var rectTool = ToolPreset.FromMarkup(rect, "Rahmen");
        Assert.Null(rectTool.Depth);
        Assert.Null(rectTool.UnitPrice);
    }

    [Fact]
    public void Bibliothek_uebersteht_Speichern_und_Laden()
    {
        var lib = ToolLibrary.Defaults().Add("Eigene", ToolPreset.FromMarkup(Area(), "Estrich") with { Favorite = true });
        var json = lib.ToJson();
        Assert.Contains("\"format\": \"e360-tools\"", json);
        Assert.Contains("\"kind\": \"Area\"", json);
        var read = ToolLibrary.FromJson(json);
        Assert.Equal(lib.Sets.Select(s => s.Name), read.Sets.Select(s => s.Name));
        Assert.Equal(lib.AllTools, read.AllTools);
    }

    [Fact]
    public void Ungueltige_Werkzeuge_werden_uebersprungen_fremde_Dateien_abgelehnt()
    {
        const string json = """
            {"format":"e360-tools","version":1,"sets":[{"name":"A","tools":[
              {"id":"1","name":"Gut","kind":"Count","stroke":"FF0000"},
              {"id":"2","name":"Zukunft","kind":"Hologramm"},
              {"id":"3","name":"Zahl","kind":"3"},
              {"id":"","name":"Ohne Id","kind":"Line"},
              {"id":"4","name":"Preis","kind":"Area","price":-5,"depth":0}]}]}
            """;
        var lib = ToolLibrary.FromJson(json);
        Assert.Equal(["Gut", "Preis"], lib.AllTools.Select(t => t.Name));
        Assert.Equal(new PdfColor(255, 0, 0), lib.AllTools.First().Style.Stroke);
        Assert.Null(lib.Find("4")!.UnitPrice);
        Assert.Null(lib.Find("4")!.Depth);

        Assert.Throws<FormatException>(() => ToolLibrary.FromJson("{\"format\":\"anders\",\"version\":1}"));
        Assert.Throws<FormatException>(() => ToolLibrary.FromJson("kein json"));
    }

    [Fact]
    public void Zusammenfuehren_verdoppelt_nichts()
    {
        var own = ToolLibrary.Empty.Add("Eigene", ToolPreset.FromMarkup(Area(), "Estrich"));
        var merged = own.Merge(ToolLibrary.Defaults());
        var count = merged.AllTools.Count();
        Assert.Equal(1 + ToolLibrary.Defaults().AllTools.Count(), count);
        Assert.Equal(count, merged.Merge(ToolLibrary.Defaults()).Merge(own).AllTools.Count());
        Assert.Equal("Eigene", merged.Sets[0].Name);
    }

    [Fact]
    public void Suche_findet_Name_Satz_und_Art_Favoriten_zuerst()
    {
        var lib = ToolLibrary.Defaults();
        var doors = lib.AllTools.First(t => t.Name == "Türen");
        lib = lib.Replace(doors with { Favorite = true });
        Assert.Equal(lib.AllTools.Count(), lib.Search("").Count);
        Assert.Equal("Türen", lib.Search("").First().Tool.Name);
        Assert.All(lib.Search("mengen hochbau"), x => Assert.Equal("Mengen Hochbau", x.Set.Name));
        Assert.Contains(lib.Search("wolke"), x => x.Tool.Name == "Mangel");
        Assert.Empty(lib.Search("Xylophon"));

        var renamed = lib.RenameSet("Planprüfung", "Prüfung");
        Assert.Contains(renamed.Sets, s => s.Name == "Prüfung");
        Assert.DoesNotContain(renamed.RemoveSet("Prüfung").Sets, s => s.Name == "Prüfung");
        Assert.Null(lib.Remove(doors.Id).Find(doors.Id));
    }
}
