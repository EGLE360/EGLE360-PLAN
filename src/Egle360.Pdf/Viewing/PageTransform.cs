using Egle360.Pdf.Markups;

namespace Egle360.Pdf.Viewing;

/// <summary>
/// Umrechnung zwischen PDF-Benutzerkoordinaten (y nach oben, ungedreht) und der Seitendarstellung (Punkt, Ursprung
/// oben links, y nach unten, mit Seitendrehung). Sichtbarer Bereich ist der Seitenrahmen aus MediaBox und CropBox.
/// </summary>
public readonly record struct PageTransform(PdfRect Box, int Rotation)
{
    /// <summary>Breite der Darstellung in Punkt.</summary>
    public double DisplayWidth => Rotation is 90 or 270 ? Box.Height : Box.Width;

    public double DisplayHeight => Rotation is 90 or 270 ? Box.Width : Box.Height;

    public (double U, double V) ToDisplay(PdfPoint p) => Rotation switch
    {
        90 => (p.Y - Box.Bottom, p.X - Box.Left),
        180 => (Box.Right - p.X, p.Y - Box.Bottom),
        270 => (Box.Top - p.Y, Box.Right - p.X),
        _ => (p.X - Box.Left, Box.Top - p.Y),
    };

    public PdfPoint ToUser(double u, double v) => Rotation switch
    {
        90 => new PdfPoint(Box.Left + v, Box.Bottom + u),
        180 => new PdfPoint(Box.Right - u, Box.Bottom + v),
        270 => new PdfPoint(Box.Right - v, Box.Top - u),
        _ => new PdfPoint(Box.Left + u, Box.Top - v),
    };

    /// <summary>
    /// Affine Abbildung Benutzer- auf Darstellungskoordinaten als (M11, M12, M21, M22, OffsetX, OffsetY) im
    /// Zeilenvektor-Schema: u = x*M11 + y*M21 + OffsetX, v = x*M12 + y*M22 + OffsetY.
    /// </summary>
    public (double M11, double M12, double M21, double M22, double OffsetX, double OffsetY) Matrix => Rotation switch
    {
        90 => (0, 1, 1, 0, -Box.Bottom, -Box.Left),
        180 => (-1, 0, 0, 1, Box.Right, -Box.Bottom),
        270 => (0, -1, -1, 0, Box.Top, Box.Right),
        _ => (1, 0, 0, -1, -Box.Left, Box.Top),
    };
}
