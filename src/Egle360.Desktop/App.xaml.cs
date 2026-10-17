using System.IO;
using System.Windows;
using Egle360.Desktop.Services;
using Egle360.Design;

namespace Egle360.Desktop;

public partial class App : Application
{
    public static ThemeManager Theme { get; private set; } = null!;

    public static ToolLibraryStore Tools { get; private set; } = new(null);

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
        Tools = screenshotDir is null ? ToolLibraryStore.CreateDefault() : new ToolLibraryStore(null);
        Tools.Load();

        var window = new MainWindow();
        if (screenshotDir is null)
        {
            DispatcherUnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, args) => WriteErrorLog(args.ExceptionObject as Exception);
            window.Show();
            window.OpenDocuments(e.Args.Where(a => a.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) && File.Exists(a)));
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

    /// <summary>Ordner für das Fehlerprotokoll, neben den Einstellungen.</summary>
    public static string ErrorLogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EGLE360", "PLAN", "Fehlerprotokoll.txt");

    /// <summary>
    /// Unerwartete Fehler in der Oberfläche: protokollieren, melden und weiterarbeiten lassen, damit ungespeicherte
    /// Änderungen nicht verloren gehen.
    /// </summary>
    private void OnUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        WriteErrorLog(e.Exception);
        MessageBox.Show(MainWindow!,
            $"Ein unerwarteter Fehler ist aufgetreten: {e.Exception.Message}\n\n"
            + $"Die Einzelheiten stehen in {ErrorLogPath}. Bitte die Arbeit speichern und die Datei bei der Rückmeldung mitschicken.",
            "EGLE360 PLAN", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private static void WriteErrorLog(Exception? ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ErrorLogPath)!);
            File.AppendAllText(ErrorLogPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} Version {typeof(App).Assembly.GetName().Version}{Environment.NewLine}{ex}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string? ArgumentAfter(string[] args, string name)
    {
        var i = Array.IndexOf(args, name);
        return i >= 0 && i + 1 < args.Length ? Path.GetFullPath(args[i + 1]) : null;
    }
}
