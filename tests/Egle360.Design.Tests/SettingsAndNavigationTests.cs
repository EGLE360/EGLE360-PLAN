using Egle360.Design;

namespace Egle360.Design.Tests;

public class SettingsAndNavigationTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "egle360-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Theory]
    [InlineData(ThemePreference.Light, false, ThemeKind.Light)]
    [InlineData(ThemePreference.Dark, true, ThemeKind.Dark)]
    [InlineData(ThemePreference.System, true, ThemeKind.Light)]
    [InlineData(ThemePreference.System, false, ThemeKind.Dark)]
    public void ResolvesTheme(ThemePreference pref, bool systemLight, ThemeKind expected)
    {
        Assert.Equal(expected, ThemeResolver.Resolve(pref, systemLight));
    }

    [Fact]
    public void SettingsRoundTrip()
    {
        var store = new UiSettingsStore(Path.Combine(_dir, "ui.json"));
        store.Save(new UiSettings { Theme = ThemePreference.Dark, NavigationCollapsed = true });
        var loaded = store.Load();
        Assert.Equal(ThemePreference.Dark, loaded.Theme);
        Assert.True(loaded.NavigationCollapsed);
        Assert.Contains("\"Dark\"", File.ReadAllText(Path.Combine(_dir, "ui.json")));
    }

    [Fact]
    public void MissingOrCorruptSettingsFallBackToDefaults()
    {
        var path = Path.Combine(_dir, "ui.json");
        Assert.Equal(ThemePreference.System, new UiSettingsStore(path).Load().Theme);
        Directory.CreateDirectory(_dir);
        File.WriteAllText(path, "{ kaputt");
        Assert.Equal(ThemePreference.System, new UiSettingsStore(path).Load().Theme);
    }

    [Fact]
    public void NavigationSelectsAndRaisesEventOnce()
    {
        var nav = new NavigationModel();
        nav.Register(new NavigationEntry("design", "Designsystem", ""));
        nav.Register(new NavigationEntry("settings", "Einstellungen", "", NavigationGroup.Footer));
        var raised = new List<string>();
        nav.SelectionChanged += (_, e) => raised.Add(e.Id);

        Assert.True(nav.Select("design"));
        Assert.True(nav.Select("design"));
        Assert.True(nav.Select("settings"));
        Assert.False(nav.Select("unbekannt"));

        Assert.Equal(new[] { "design", "settings" }, raised);
        Assert.Equal("settings", nav.Selected!.Id);
    }

    [Fact]
    public void NavigationRejectsDuplicateIds()
    {
        var nav = new NavigationModel();
        nav.Register(new NavigationEntry("a", "A", "x"));
        Assert.Throws<InvalidOperationException>(() => nav.Register(new NavigationEntry("a", "B", "y")));
    }
}
