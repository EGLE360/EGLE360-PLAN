using System.Text.Json;
using System.Xml.Linq;
using Egle360.Design;

namespace Egle360.Design.Tests;

public class BrandColorTests
{
    // Unabhängige Abschrift der Tabellen aus Abschnitt 28 des Master-Prompts.
    public static TheoryData<string, string, string> MasterPromptColors => new()
    {
        { "logo", "LogoGreen", "#45A04B" },
        { "logo", "LogoBlue", "#2283D6" },
        { "logo", "LogoLightGray", "#BAC2CA" },
        { "logo", "LogoWhite", "#FFFFFF" },
        { "brand", "BrandDarkBlue", "#102A43" },
        { "brand", "BrandGreen", "#194D23" },
        { "brand", "BrandAnthracite", "#4A4F57" },
        { "brand", "BrandBlueAccent", "#1677D2" },
        { "navigation", "NavBackground", "#172532" },
        { "navigation", "NavHover", "#1D3040" },
        { "navigation", "NavSelected", "#135E9F" },
        { "Dark", "Canvas", "#0B121A" },
        { "Dark", "Surface1", "#111C27" },
        { "Dark", "Surface2", "#172431" },
        { "Dark", "Border", "#293847" },
        { "Dark", "Text", "#F3F6F9" },
        { "Dark", "TextSecondary", "#9EACBA" },
        { "Dark", "Accent", "#3395E8" },
        { "Dark", "Success", "#43B96C" },
        { "Dark", "Warning", "#F0AE32" },
        { "Dark", "Critical", "#E85C5C" },
        { "Light", "Canvas", "#F3F5F7" },
        { "Light", "Surface1", "#FFFFFF" },
        { "Light", "Border", "#D8E0E8" },
        { "Light", "Text", "#17212B" },
        { "Light", "TextSecondary", "#66727D" },
        { "Light", "Accent", "#1677D2" },
        { "Light", "Success", "#2E9D59" },
        { "Light", "Warning", "#E5A323" },
        { "Light", "Critical", "#D94A4A" },
    };

    [Theory]
    [MemberData(nameof(MasterPromptColors))]
    public void TokensMatchMasterPrompt(string group, string key, string expected)
    {
        Assert.Equal(expected, Repo.Token(group, key), ignoreCase: true);
    }

    [Theory]
    [InlineData("Brand.xaml", "logo")]
    [InlineData("Brand.xaml", "brand")]
    [InlineData("Brand.xaml", "navigation")]
    [InlineData("Colors.Light.xaml", "Light")]
    [InlineData("Colors.Dark.xaml", "Dark")]
    public void GeneratedXamlMatchesTokens(string file, string group)
    {
        var doc = XDocument.Load(Path.Combine(Repo.Root, "src", "Egle360.Desktop", "Themes", file));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        var colors = doc.Root!.Elements()
            .Where(e => e.Name.LocalName == "Color")
            .ToDictionary(e => (string)e.Attribute(x + "Key")!, e => e.Value);

        foreach (var (key, hex) in Repo.Group(group))
        {
            Assert.True(colors.TryGetValue(key + "Color", out var argb), $"{key}Color fehlt in {file}");
            Assert.Equal("#FF" + hex.TrimStart('#'), argb, ignoreCase: true);
        }
    }

    [Fact]
    public void LightAndDarkDefineTheSameKeys()
    {
        Assert.Equal(Repo.Group("Light").Keys.Order(), Repo.Group("Dark").Keys.Order());
    }

    [Fact]
    public void GeneratedCssContainsEveryThemeValue()
    {
        var css = File.ReadAllText(Path.Combine(Repo.Root, "design", "tokens.css"));
        foreach (var group in new[] { "logo", "brand", "navigation", "Light", "Dark" })
        {
            foreach (var hex in Repo.Group(group).Values)
            {
                Assert.Contains(hex, css, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}

internal static class Repo
{
    public static string Root { get; } = FindRoot();

    private static readonly JsonElement Tokens =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "design", "tokens.json"))).RootElement;

    public static Dictionary<string, string> Group(string group)
    {
        var el = group is "Light" or "Dark" ? Tokens.GetProperty("themes").GetProperty(group) : Tokens.GetProperty(group);
        return el.EnumerateObject().ToDictionary(p => p.Name, p => p.Value.GetString()!);
    }

    public static string Token(string group, string key) => Group(group)[key];

    public static RgbColor Color(string group, string key) => RgbColor.Parse(Token(group, key));

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "EGLE360.PLAN.sln")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Repository-Stamm nicht gefunden.");
    }
}
