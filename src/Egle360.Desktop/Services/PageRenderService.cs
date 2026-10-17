using System.Collections.Concurrent;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Egle360.Pdf;

namespace Egle360.Desktop.Services;

public enum RenderPriority
{
    /// <summary>Sichtbare Seiten und Detailausschnitte.</summary>
    View,

    /// <summary>Miniaturen und Seiten knapp außerhalb des Fensters.</summary>
    Background,
}

/// <summary>
/// Rendert Seiten auf einem eigenen Hintergrund-Thread, damit die Oberfläche nie auf PDFium wartet.
/// PDFium arbeitet ohnehin nur einen Auftrag gleichzeitig ab; ein einzelner Thread vermeidet Wartezeiten an der Sperre.
/// Abgebrochene Aufträge (z. B. nach dem Wegscrollen) werden übersprungen.
/// </summary>
public sealed class PageRenderService : IDisposable
{
    private readonly ConcurrentQueue<Job> _view = new();
    private readonly ConcurrentQueue<Job> _background = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _stop = new();
    private readonly Thread _worker;
    private int _pending;

    public PageRenderService()
    {
        _worker = new Thread(Run) { IsBackground = true, Name = "EGLE360 Seitendarstellung" };
        _worker.Start();
    }

    /// <summary>Anzahl offener Aufträge. Der Screenshot-Modus wartet, bis sie null ist.</summary>
    public int Pending => Volatile.Read(ref _pending);

    /// <summary>Rendert einen Ausschnitt und liefert ein eingefrorenes Bild, oder null bei Abbruch oder Fehler.</summary>
    /// <param name="hideMarkups">EGLE360-Markierungen weglassen, weil die Ansicht sie selbst zeichnet.</param>
    public Task<BitmapSource?> RenderAsync(PdfDocument document, int page, double scale, PixelRect region, RenderPriority priority, CancellationToken cancel, bool hideMarkups = false)
    {
        var job = new Job(document, page, scale, region, cancel, hideMarkups);
        Interlocked.Increment(ref _pending);
        (priority == RenderPriority.View ? _view : _background).Enqueue(job);
        _signal.Release();
        return job.Completion.Task;
    }

    private void Run()
    {
        while (!_stop.IsCancellationRequested)
        {
            try
            {
                _signal.Wait(_stop.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            if (!_view.TryDequeue(out var job) && !_background.TryDequeue(out job))
            {
                continue;
            }
            try
            {
                job.Completion.TrySetResult(job.Cancel.IsCancellationRequested ? null : Render(job));
            }
            catch (Exception ex) when (ex is PdfException or ObjectDisposedException or ArgumentException or OutOfMemoryException)
            {
                // Eine fehlerhafte Seite darf die Darstellung der übrigen nicht aufhalten.
                System.Diagnostics.Debug.WriteLine($"Seite {job.Page + 1} nicht darstellbar: {ex.Message}");
                job.Completion.TrySetResult(null);
            }
            finally
            {
                Interlocked.Decrement(ref _pending);
            }
        }
    }

    private static BitmapSource? Render(Job job)
    {
        var r = job.Region;
        if (r.Width <= 0 || r.Height <= 0)
        {
            return null;
        }
        var stride = r.Width * 4;
        var buffer = new byte[stride * r.Height];
        job.Document.Render(job.Page, job.Scale, r, buffer, stride, hideMarkups: job.HideMarkups);
        if (job.Cancel.IsCancellationRequested)
        {
            return null;
        }
        var bitmap = BitmapSource.Create(r.Width, r.Height, 96, 96, PixelFormats.Bgra32, null, buffer, stride);
        bitmap.Freeze();
        return bitmap;
    }

    public void Dispose()
    {
        _stop.Cancel();
        _worker.Join(TimeSpan.FromSeconds(2));
    }

    private sealed record Job(PdfDocument Document, int Page, double Scale, PixelRect Region, CancellationToken Cancel, bool HideMarkups)
    {
        public TaskCompletionSource<BitmapSource?> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
