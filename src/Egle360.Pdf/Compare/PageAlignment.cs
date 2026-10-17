using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Compare;

/// <summary>
/// Lage des neuen Stands gegenüber dem alten in Anzeigekoordinaten der Seite (Punkt, x nach rechts, y nach unten):
/// u' = A·u + C·v + E, v' = B·u + D·v + F. Ein Punkt des alten Stands liegt im neuen an der abgebildeten Stelle.
/// </summary>
public sealed record PageAlignment(double A, double B, double C, double D, double E, double F)
{
    /// <summary>Kleinster Abstand der beiden Passpunkte in Punkt, darunter wird Drehung und Maßstab zu ungenau.</summary>
    public const double MinPointDistance = 20;

    public static PageAlignment Identity { get; } = Translation(0, 0);

    public static PageAlignment Translation(double dx, double dy) => new(1, 0, 0, 1, dx, dy);

    /// <summary>Reine Verschiebung aus einem Passpunkt im alten und demselben Punkt im neuen Stand.</summary>
    public static PageAlignment FromPoints(PdfPoint old1, PdfPoint new1) => Translation(new1.X - old1.X, new1.Y - old1.Y);

    /// <summary>
    /// Verschiebung, Drehung und gleichmäßiger Maßstab aus zwei Passpunkten. Null, wenn die Punkte zu nah beieinander
    /// liegen oder der Maßstab außerhalb von 1:5 bis 5:1 läge (dann wurde vermutlich ein falscher Punkt gewählt).
    /// </summary>
    public static PageAlignment? FromPoints(PdfPoint old1, PdfPoint new1, PdfPoint old2, PdfPoint new2)
    {
        double px = old2.X - old1.X, py = old2.Y - old1.Y;
        double qx = new2.X - new1.X, qy = new2.Y - new1.Y;
        var len2 = px * px + py * py;
        if (Math.Sqrt(len2) < MinPointDistance || Math.Sqrt(qx * qx + qy * qy) < MinPointDistance)
        {
            return null;
        }
        // Als komplexe Zahl: (qx + i·qy) / (px + i·py) = a + i·b, also Drehung und Maßstab in einem.
        var a = (qx * px + qy * py) / len2;
        var b = (qy * px - qx * py) / len2;
        var factor = Math.Sqrt(a * a + b * b);
        if (factor < 0.2 || factor > 5)
        {
            return null;
        }
        var e = new1.X - (a * old1.X - b * old1.Y);
        var f = new1.Y - (b * old1.X + a * old1.Y);
        return new PageAlignment(a, b, -b, a, e, f);
    }

    public bool IsTranslation => A == 1 && B == 0 && C == 0 && D == 1;

    /// <summary>Drehung in Grad (im Uhrzeigersinn positiv, weil y nach unten zeigt).</summary>
    public double RotationDegrees => Math.Atan2(B, A) * 180 / Math.PI;

    public double Factor => Math.Sqrt(A * A + B * B);

    public (double U, double V) Map(double u, double v) => (A * u + C * v + E, B * u + D * v + F);

    /// <summary>Kurzbeschreibung für die Statuszeile, in Millimetern und Grad.</summary>
    public string Describe()
    {
        const double mm = 25.4 / 72;
        var text = $"Verschiebung {E * mm:0.#} mm waagerecht und {F * mm:0.#} mm senkrecht";
        if (!IsTranslation)
        {
            text += $", Drehung {RotationDegrees:0.##}°, Maßstab {Factor:0.####}";
        }
        return text;
    }
}
