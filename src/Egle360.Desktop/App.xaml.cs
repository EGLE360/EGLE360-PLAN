using System.IO;
using System.Windows;
using Egle360.Desktop.Services;
using Egle360.Design;

namespace Egle360.Desktop;

public partial class App : Application
{
    public static ThemeManager Theme { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var screenshotDir = ArgumentAfter(e.Args, "--screenshots");

        // Im Screenshot-Modus werden die Einstellungen des Benutzers nicht verändert.
        var store = screenshotDir is null
            ? UiSettingsStore.CreateDefault()
            : new UiSettingsStore(Path.Combine(Path.GetTempPath(), $"egle360-screenshots-{Environment.ProcessId}.json"));
        Theme = new ThemeManager(this, store);
        Theme.Initialize();

        var window = new MainWindow();
        if (screenshotDir is null)
        {
            window.Show();
            return;
        }

        try
        {
            Shutdown(ScreenshotRunner.Run(window, screenshotDir));
        }
        catch (Exception ex)
        {
            File.WriteAllText(Path.Combine(screenshotDir, "error.txt"), ex.ToString());
            Shutdown(1);
        }
    }

    private static string? ArgumentAfter(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
    }
}
