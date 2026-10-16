using System.Globalization;

namespace Egle360.Design;

/// <summary>Opake sRGB-Farbe mit Kontrastberechnung nach WCAG 2.1.</summary>
public readonly record struct RgbColor(byte R, byte G, byte B)
{
    public static RgbColor Parse(string hex)
    {
        ArgumentNullException.ThrowIfNull(hex);
        var s = hex.Trim().TrimStart('#');
        if (s.Length == 8 && s.StartsWith("FF", StringComparison.OrdinalIgnoreCase))
        {
            s = s[2..];
        }
        if (s.Length != 6 || !int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
        {
            throw new FormatException($"Keine gültige opake Hex-Farbe: '{hex}'");
        }
        return new RgbColor((byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }

    /// <summary>Relative Leuchtdichte nach WCAG 2.1, Abschnitt 1.4.3.</summary>
    public double RelativeLuminance =>
        0.2126 * Linearize(R) + 0.7152 * Linearize(G) + 0.0722 * Linearize(B);

    public static double ContrastRatio(RgbColor a, RgbColor b)
    {
        var la = a.RelativeLuminance;
        var lb = b.RelativeLuminance;
        var (hi, lo) = la >= lb ? (la, lb) : (lb, la);
        return (hi + 0.05) / (lo + 0.05);
    }

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    private static double Linearize(byte channel)
    {
        var c = channel / 255.0;
        return c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    }
}
