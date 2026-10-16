namespace Egle360.Pdf.Viewing;

/// <summary>
/// Entscheidet, in welcher Auflösung eine Seite gerendert wird. Jede sichtbare Seite erhält ein Grundbild mit
/// begrenzter Pixelzahl. Reicht dessen Auflösung beim aktuellen Zoom nicht aus, wird zusätzlich nur der sichtbare
/// Ausschnitt scharf gerendert. So bleibt der Speicherbedarf auch bei A0-Plänen und hohem Zoom begrenzt.
/// </summary>
public static class RenderPlanner
{
    /// <summary>Höchstzahl der Pixel eines Grundbilds (4 MP, etwa 16 MB).</summary>
    public const double MaxBasePixels = 4_000_000;

    /// <summary>Pixel je Punkt, die bei diesem Zoom und dieser Bildschirmskalierung gebraucht werden.</summary>
    public static double RequiredScale(double zoom, double pixelsPerDip) => zoom * PageLayout.DipPerPoint * pixelsPerDip;

    /// <summary>Maßstab des Grundbilds: so scharf wie nötig, höchstens <see cref="MaxBasePixels"/>.</summary>
    public static double BaseScale(double widthPt, double heightPt, double requiredScale)
    {
        var cap = Math.Sqrt(MaxBasePixels / (widthPt * heightPt));
        return Math.Min(requiredScale, cap);
    }

    /// <summary>Ein vorhandenes Grundbild genügt, wenn es nicht merklich unscharf und nicht unnötig groß ist.</summary>
    public static bool BaseIsGoodEnough(double existingScale, double wantedScale) =>
        existingScale >= wantedScale * 0.95 && existingScale <= wantedScale * 1.6;

    /// <summary>Ob für den sichtbaren Ausschnitt ein schärferes Detailbild nötig ist.</summary>
    public static bool NeedsDetail(double baseScale, double requiredScale) => requiredScale > baseScale * 1.05;

    /// <summary>
    /// Pixelausschnitt für das Detailbild. <paramref name="visiblePt"/> ist der sichtbare Teil der Seite in Punkt
    /// (oben links). Der Ausschnitt wird auf ganze Pixel nach außen gerundet und auf die Seite begrenzt.
    /// </summary>
    public static PixelRect DetailRegion(LayoutRect visiblePt, double scale, int pagePixelWidth, int pagePixelHeight)
    {
        var x0 = Math.Clamp((int)Math.Floor(visiblePt.X * scale), 0, pagePixelWidth);
        var y0 = Math.Clamp((int)Math.Floor(visiblePt.Y * scale), 0, pagePixelHeight);
        var x1 = Math.Clamp((int)Math.Ceiling(visiblePt.Right * scale), 0, pagePixelWidth);
        var y1 = Math.Clamp((int)Math.Ceiling(visiblePt.Bottom * scale), 0, pagePixelHeight);
        return new PixelRect(x0, y0, x1 - x0, y1 - y0);
    }
}
