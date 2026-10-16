using System.Security.Cryptography;
using System.Xml.Linq;

namespace Egle360.Design.Tests;

/// <summary>
/// Sichert ab, dass die Logodateien unverändert bleiben und je Fläche die in tokens.json festgelegte Variante erscheint.
/// </summary>
public class LogoAssetTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static TheoryData<string, string> RecordedHashes()
    {
        var data = new TheoryData<string, string>();
        foreach (var line in File.ReadAllLines(Path.Combine(Repo.Root, "brand", "SHA256SUMS")).Where(l => l.Length > 0))
        {
            var parts = line.Split("  ", 2);
            data.Add(parts[1], parts[0]);
        }
        return data;
    }

    [Theory]
    [MemberData(nameof(RecordedHashes))]
    public void BrandFilesAreUnchanged(string file, string sha256)
    {
        var actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(Repo.Root, "brand", file))));
        Assert.Equal(sha256, actual);
    }

    [Fact]
    public void EveryUsedLogoIsRecordedAndEmbedded()
    {
        var recorded = File.ReadAllText(Path.Combine(Repo.Root, "brand", "SHA256SUMS"));
        var csproj = File.ReadAllText(Path.Combine(Repo.Root, "src", "Egle360.Desktop", "Egle360.Desktop.csproj"));
        foreach (var (usage, file) in Repo.Group("logoUsage"))
        {
            Assert.Contains("  " + file, recorded);
            Assert.Contains($@"Include=""..\..\brand\{file}""", csproj);
        }
    }

    [Theory]
    [InlineData("Brand.xaml", "LogoNavigationImage", "Navigation")]
    [InlineData("Brand.xaml", "LogoReportImage", "Report")]
    [InlineData("Colors.Light.xaml", "LogoContentImage", "Light")]
    [InlineData("Colors.Dark.xaml", "LogoContentImage", "Dark")]
    public void GeneratedXamlUsesAssignedLogo(string file, string key, string usage)
    {
        var doc = XDocument.Load(Path.Combine(Repo.Root, "src", "Egle360.Desktop", "Themes", file));
        var image = doc.Root!.Elements().Single(e => (string?)e.Attribute(X + "Key") == key);
        Assert.EndsWith("/Assets/" + Repo.Token("logoUsage", usage), (string)image.Attribute("UriSource")!);
    }
}
