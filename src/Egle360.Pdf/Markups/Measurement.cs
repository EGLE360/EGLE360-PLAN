using System.Globalization;

namespace Egle360.Pdf.Markups;

/// <summary>Längeneinheit, in der Messwerte angezeigt werden.</summary>
public enum MeasureUnit
{
    Meter,
    Centimeter,
    Millimeter,
}

/// <summary>
/// Maßstab einer Seite oder Messung: wie viele Meter in der Wirklichkeit einem PDF-Punkt (1/72 Zoll) auf dem Papier
/// entsprechen, dazu die Anzeigeeinheit und eine Bezeichnung wie „1:50“ oder „Kalibriert“.
/// </summary>
public sealed record MeasureScale(double MetersPerPoint, MeasureUnit Unit, string Label)
{
    /// <summary>Ein PDF-Punkt auf dem Papier in Metern.</summary>
    public const double PaperMetersPerPoint = 0.0254 / 72;

    /// <summary>Übliche Planmaßstäbe im Bauwesen.</summary>
    public static IReadOnlyList<int> CommonRatios { get; } = [1, 5, 10, 20, 25, 50, 100, 200, 250, 500, 1000, 2000];

    /// <summary>Maßstab 1:<paramref name="ratio"/> (Papiermaß mal Verhältnis).</summary>
    public static MeasureScale FromRatio(double ratio, MeasureUnit unit = MeasureUnit.Meter) =>
        new(PaperMetersPerPoint * ratio, unit, $"1:{ratio.ToString("0.##", CultureInfo.InvariantCulture)}");

    /// <summary>Maßstab aus einer bekannten Strecke: <paramref name="points"/> auf dem Papier entsprechen <paramref name="realMeters"/>.</summary>
    public static MeasureScale Calibrated(double points, double realMeters, MeasureUnit unit)
    {
        if (points <= 0 || realMeters <= 0 || !double.IsFinite(points) || !double.IsFinite(realMeters))
        {
            throw new ArgumentOutOfRangeException(nameof(points), "Strecke und Maß müssen größer als null sein.");
        }
        var mpp = realMeters / points;
        // Das errechnete Verhältnis hilft beim Gegenprüfen mit dem Planstempel.
        var ratio = mpp / PaperMetersPerPoint;
        return new MeasureScale(mpp, unit, $"Kalibriert, etwa 1:{ratio.ToString(ratio >= 10 ? "0" : "0.#", CultureInfo.GetCultureInfo("de-DE"))}");
    }

    /// <summary>Faktor von Metern in die Anzeigeeinheit.</summary>
    public double UnitFactor => Measurement.Factor(Unit);

    public string UnitSymbol => Measurement.Symbol(Unit);
}

/// <summary>Messwert einer Messung in der Anzeigeeinheit des Maßstabs.</summary>
/// <param name="Value">Hauptwert: Länge, Fläche oder Stückzahl.</param>
/// <param name="Unit">Einheit des Hauptwerts (z. B. „m“, „m²“, „Stk.“).</param>
/// <param name="Perimeter">Umfang bei Flächen, sonst null.</param>
public readonly record struct MeasureValue(double Value, string Unit, double? Perimeter = null, string? PerimeterUnit = null);

/// <summary>Berechnung und Formatierung von Messwerten.</summary>
public static class Measurement
{
    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

    public static string Symbol(MeasureUnit unit) => unit switch
    {
        MeasureUnit.Centimeter => "cm",
        MeasureUnit.Millimeter => "mm",
        _ => "m",
    };

    /// <summary>Faktor von Metern in die Einheit.</summary>
    public static double Factor(MeasureUnit unit) => unit switch
    {
        MeasureUnit.Centimeter => 100,
        MeasureUnit.Millimeter => 1000,
        _ => 1,
    };

    /// <summary>Nachkommastellen je Einheit, wie in Mengenermittlungen üblich.</summary>
    public static int Decimals(MeasureUnit unit) => unit switch
    {
        MeasureUnit.Centimeter => 1,
        MeasureUnit.Millimeter => 0,
        _ => 2,
    };

    /// <summary>Messwert einer Markierung; null, wenn es keine Messung ist oder der Maßstab fehlt (außer bei Anzahl).</summary>
    public static MeasureValue? Compute(Markup m)
    {
        if (!MarkupKinds.IsMeasurement(m.Kind))
        {
            return null;
        }
        var pts = m.Points;
        if (m.Kind == MarkupKind.Count)
        {
            return new MeasureValue(pts.Count, "Stk.");
        }
        if (m.Kind == MarkupKind.Angle)
        {
            return pts.Count >= 3 ? new MeasureValue(AngleDegrees(pts[0], pts[1], pts[2]), "°") : null;
        }
        if (m.Scale is not { } scale)
        {
            return null;
        }
        var f = scale.MetersPerPoint * scale.UnitFactor;
        var sym = scale.UnitSymbol;
        return m.Kind switch
        {
            MarkupKind.Length or MarkupKind.Polylength or MarkupKind.Dimension => new MeasureValue(PathLength(pts, closed: false) * f, sym),
            MarkupKind.Perimeter => new MeasureValue(PathLength(pts, closed: true) * f, sym),
            MarkupKind.Area => new MeasureValue(NetArea(m) * f * f, sym + "²", PathLength(pts, closed: true) * f, sym),
            _ => null,
        };
    }

    /// <summary>
    /// Abgeleiteter Wert aus Höhe oder Tiefe: Volumen in m³ bei Flächen, Wandfläche in m² bei Längen, Polylängen
    /// und Umfängen. Immer in Metern, unabhängig von der Anzeigeeinheit, weil Mengen so abgerechnet werden.
    /// </summary>
    public static (double Value, string Unit, string Name)? Derived(Markup m)
    {
        if (m.Depth is not { } depth || depth <= 0 || m.Scale is not { } s)
        {
            return null;
        }
        var mpp = s.MetersPerPoint;
        return m.Kind switch
        {
            MarkupKind.Area => (NetArea(m) * mpp * mpp * depth, "m³", "Volumen"),
            MarkupKind.Length or MarkupKind.Polylength => (PathLength(m.Points, false) * mpp * depth, "m²", "Wandfläche"),
            MarkupKind.Perimeter => (PathLength(m.Points, true) * mpp * depth, "m²", "Wandfläche"),
            _ => null,
        };
    }

    /// <summary>Messarten, die eine Höhe oder Tiefe haben können.</summary>
    public static bool SupportsDepth(MarkupKind kind) => kind is MarkupKind.Area or MarkupKind.Length or MarkupKind.Polylength or MarkupKind.Perimeter;

    /// <summary>Winkel am Scheitel <paramref name="vertex"/> zwischen den Schenkeln zu <paramref name="a"/> und <paramref name="b"/>, 0 bis 180 Grad.</summary>
    public static double AngleDegrees(PdfPoint a, PdfPoint vertex, PdfPoint b)
    {
        var u = a - vertex;
        var v = b - vertex;
        if (u.Length < 1e-9 || v.Length < 1e-9)
        {
            return 0;
        }
        var cos = Math.Clamp((u.X * v.X + u.Y * v.Y) / (u.Length * v.Length), -1, 1);
        return Math.Acos(cos) * 180 / Math.PI;
    }

    /// <summary>Fläche in Punkt² ohne die Ausschnitte (weitere Pfade der Markierung).</summary>
    public static double NetArea(Markup m)
    {
        var outer = Math.Abs(MarkupGeometry.SignedArea(m.Points));
        var holes = m.Paths.Skip(1).Where(p => p.Count >= 3).Sum(p => Math.Abs(MarkupGeometry.SignedArea(p)));
        return Math.Max(0, outer - holes);
    }

    public static double PathLength(IReadOnlyList<PdfPoint> pts, bool closed)
    {
        var sum = 0.0;
        for (var i = 1; i < pts.Count; i++)
        {
            sum += pts[i - 1].DistanceTo(pts[i]);
        }
        if (closed && pts.Count > 2)
        {
            sum += pts[^1].DistanceTo(pts[0]);
        }
        return sum;
    }

    /// <summary>Zahl im deutschen Format mit fester Anzahl Nachkommastellen, z. B. „1.234,50“.</summary>
    public static string Number(double value, int decimals) => value.ToString("N" + decimals, German);

    /// <summary>Beschriftung auf dem Plan, z. B. „4,25 m“, „12,40 m²“ oder „7 Stk.“.</summary>
    public static string Label(Markup m)
    {
        if (Compute(m) is not { } v)
        {
            return MarkupKinds.IsMeasurement(m.Kind) ? "kein Maßstab" : "";
        }
        if (m.Kind == MarkupKind.Count)
        {
            return $"{v.Value:0} Stk.";
        }
        if (m.Kind == MarkupKind.Angle)
        {
            return $"{Number(v.Value, 1)}°";
        }
        var d = Decimals(m.Scale!.Unit);
        var main = $"{Number(v.Value, d)} {v.Unit}";
        return Derived(m) is { } x ? $"{main}, {Number(x.Value, 2)} {x.Unit}" : main;
    }

    /// <summary>Ausführlicher Text für Listen und Statuszeile, bei Flächen mit Umfang.</summary>
    public static string Describe(Markup m)
    {
        if (Compute(m) is not { } v || m.Scale is not { } s || m.Kind is MarkupKind.Count or MarkupKind.Angle)
        {
            return Label(m);
        }
        var text = $"{Number(v.Value, Decimals(s.Unit))} {v.Unit}";
        if (v is { Perimeter: { } p, PerimeterUnit: { } pu })
        {
            text += $", Umfang {Number(p, Decimals(s.Unit))} {pu}";
        }
        if (Derived(m) is { } x)
        {
            text += $", {x.Name} {Number(x.Value, 2)} {x.Unit} bei {(m.Kind == MarkupKind.Area ? "Tiefe" : "Höhe")} {Number(m.Depth!.Value, 2)} m";
        }
        var cutouts = m.Paths.Count - 1;
        if (m.Kind == MarkupKind.Area && cutouts > 0)
        {
            text += cutouts == 1 ? ", 1 Ausschnitt abgezogen" : $", {cutouts} Ausschnitte abgezogen";
        }
        return text;
    }

    /// <summary>Liest eine Zahl in deutscher oder englischer Schreibweise („2,5“, „2.5“, „1.250,5“).</summary>
    public static bool TryParseNumber(string text, out double value)
    {
        text = text.Trim().Replace(" ", "");
        if (text.Contains(','))
        {
            return double.TryParse(text, NumberStyles.Float | NumberStyles.AllowThousands, German, out value) && value > 0;
        }
        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && value > 0;
    }
}
