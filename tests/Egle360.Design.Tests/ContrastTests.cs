using Egle360.Design;

namespace Egle360.Design.Tests;

/// <summary>
/// Prüft die Farbpaare, die das Designsystem für Text zulässt, gegen WCAG 2.1 AA (4,5:1).
/// Die Nutzungsregeln stehen in docs/phase3/EGLE360_PLAN_Designsystem.md.
/// </summary>
public class ContrastTests
{
    private const double NormalText = 4.5;

    [Fact]
    public void ReferenceValuesMatchWcag()
    {
        Assert.Equal(21.0, RgbColor.ContrastRatio(RgbColor.Parse("#000000"), RgbColor.Parse("#FFFFFF")), 6);
        Assert.Equal(1.0, RgbColor.ContrastRatio(RgbColor.Parse("#777777"), RgbColor.Parse("#777777")), 6);
        // Bekannter Grenzwert: #767676 auf Weiß erfüllt 4,5:1 knapp.
        Assert.InRange(RgbColor.ContrastRatio(RgbColor.Parse("#767676"), RgbColor.Parse("#FFFFFF")), 4.54, 4.55);
    }

    [Theory]
    [InlineData("Light", "Text", "Canvas")]
    [InlineData("Light", "Text", "Surface1")]
    [InlineData("Light", "Text", "Surface2")]
    [InlineData("Light", "TextSecondary", "Canvas")]
    [InlineData("Light", "TextSecondary", "Surface1")]
    [InlineData("Light", "TextSecondary", "Surface2")]
    [InlineData("Light", "Accent", "Surface1")]
    [InlineData("Dark", "Text", "Canvas")]
    [InlineData("Dark", "Text", "Surface1")]
    [InlineData("Dark", "Text", "Surface2")]
    [InlineData("Dark", "TextSecondary", "Canvas")]
    [InlineData("Dark", "TextSecondary", "Surface1")]
    [InlineData("Dark", "TextSecondary", "Surface2")]
    [InlineData("Dark", "Accent", "Canvas")]
    [InlineData("Dark", "Accent", "Surface1")]
    [InlineData("Dark", "Accent", "Surface2")]
    [InlineData("Dark", "Success", "Surface1")]
    [InlineData("Dark", "Warning", "Surface1")]
    [InlineData("Dark", "Critical", "Surface1")]
    public void ThemeTextPairsMeetAa(string theme, string fg, string bg)
    {
        var ratio = RgbColor.ContrastRatio(Repo.Color(theme, fg), Repo.Color(theme, bg));
        Assert.True(ratio >= NormalText, $"{theme}: {fg} auf {bg} = {ratio:F2}");
    }

    [Theory]
    [InlineData("NavText", "NavBackground")]
    [InlineData("NavText", "NavHover")]
    [InlineData("NavText", "NavSelected")]
    [InlineData("NavTextSecondary", "NavBackground")]
    [InlineData("NavTextSecondary", "NavHover")]
    public void NavigationTextPairsMeetAa(string fg, string bg)
    {
        var ratio = RgbColor.ContrastRatio(Repo.Color("navigation", fg), Repo.Color("navigation", bg));
        Assert.True(ratio >= NormalText, $"{fg} auf {bg} = {ratio:F2}");
    }

    [Theory]
    [InlineData("Light")]
    [InlineData("Dark")]
    public void OnAccentMeetsAa(string theme)
    {
        var ratio = RgbColor.ContrastRatio(Repo.Color(theme, "OnAccent"), Repo.Color(theme, "Accent"));
        Assert.True(ratio >= NormalText, $"{theme}: OnAccent auf Accent = {ratio:F2}");
    }

    [Fact]
    public void WhiteOnDarkAccentWouldFail()
    {
        // Begründung, warum OnAccent im Dark Mode nicht Weiß ist.
        Assert.True(RgbColor.ContrastRatio(RgbColor.Parse("#FFFFFF"), Repo.Color("Dark", "Accent")) < NormalText);
    }

    [Theory]
    [InlineData("Success")]
    [InlineData("Warning")]
    [InlineData("Critical")]
    public void LightStatusColorsAreNotUsedAsText(string status)
    {
        // Dokumentiert die Regel: Im Light Mode sind Statusfarben nur Kennzeichen, die Beschriftung steht in Text-Farbe.
        var ratio = RgbColor.ContrastRatio(Repo.Color("Light", status), Repo.Color("Light", "Surface1"));
        Assert.True(ratio < NormalText, $"{status} erreicht {ratio:F2}; Regel im Designsystem anpassen.");
    }

    [Theory]
    [InlineData("#45a04b")]
    [InlineData("45A04B")]
    [InlineData("#FF45A04B")]
    public void ParseAcceptsCommonForms(string hex)
    {
        Assert.Equal(new RgbColor(0x45, 0xA0, 0x4B), RgbColor.Parse(hex));
    }

    [Theory]
    [InlineData("#12345")]
    [InlineData("#80FFFFFF")]
    [InlineData("zzzzzz")]
    public void ParseRejectsInvalidOrTransparent(string hex)
    {
        Assert.Throws<FormatException>(() => RgbColor.Parse(hex));
    }
}
