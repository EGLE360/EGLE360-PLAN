using Egle360.Pdf;
using Egle360.Pdf.Markups;

namespace Egle360.Desktop.Services;

/// <summary>
/// Erzeugt einen frei erfundenen Beispielplan für den Screenshot-Modus: Grundriss im Maßstab 1:50 auf A1 quer
/// und einen Schnitt auf A3 quer. Alle Inhalte sind Platzhalter und stellen kein reales Projekt dar.
/// </summary>
internal static class SamplePlan
{
    private const double M = 72 / 25.4 * 1000 / 50; // 1 m im Maßstab 1:50 in Punkt

    private static readonly PdfColor Wall = new(30, 30, 30);
    private static readonly PdfColor Grid = new(150, 150, 150);
    private static readonly PdfColor Dimension = new(22, 119, 210);

    public static void Write(string path, ITextOutliner? outliner = null)
    {
        using var doc = PdfDocument.CreateNew();
        var (a1Short, a1Long) = PaperFormat.IsoSizePoints("A1");
        var (a3Short, a3Long) = PaperFormat.IsoSizePoints("A3");
        doc.InsertBlankPage(0, a1Long, a1Short);
        doc.InsertBlankPage(1, a3Long, a3Short);

        using (var c = doc.EditContent(0))
        {
            Floor(c, a1Long, a1Short);
            c.Commit();
        }
        using (var c = doc.EditContent(1))
        {
            Section(c, a3Long, a3Short);
            c.Commit();
        }
        doc.SetPageScale(0, MeasureScale.FromRatio(50));
        doc.SetPageScale(1, MeasureScale.FromRatio(100));
        if (outliner is not null)
        {
            doc.SetMarkups(0, [.. Markups(a1Long), .. Measurements()], outliner);
        }
        doc.SaveAs(path);
    }

    private static void Floor(PdfContentWriter c, double pageW, double pageH)
    {
        c.Rectangle(28, 28, pageW - 56, pageH - 56, Wall, null, 1.5);
        const double ox = 220, oy = 360; // Ursprung des Grundrisses
        const double w = 30, h = 16;      // Gebäude 30 m × 16 m

        // Achsraster
        string[] axesX = ["A", "B", "C", "D", "E", "F", "G"];
        for (var i = 0; i < axesX.Length; i++)
        {
            var x = ox + i * 5 * M;
            c.Line(x, oy - 70, x, oy + h * M + 70, Grid, 0.5);
            c.Text(x - 5, oy + h * M + 80, axesX[i], 16, Grid);
        }
        for (var j = 0; j <= 4; j++)
        {
            var y = oy + j * 4 * M;
            c.Line(ox - 70, y, ox + w * M + 70, y, Grid, 0.5);
            c.Text(ox - 95, y - 5, (j + 1).ToString(), 16, Grid);
        }

        // Außenwände 30 cm
        const double t = 0.3 * M;
        c.Rectangle(ox, oy, w * M, h * M, Wall, null, 3);
        c.Rectangle(ox + t, oy + t, w * M - 2 * t, h * M - 2 * t, Wall, null, 1.5);

        // Innenwände
        c.Line(ox + 10 * M, oy, ox + 10 * M, oy + h * M, Wall, 2);
        c.Line(ox + 20 * M, oy, ox + 20 * M, oy + h * M, Wall, 2);
        c.Line(ox, oy + 6 * M, ox + w * M, oy + 6 * M, Wall, 2);
        c.Line(ox + 20 * M, oy + 11 * M, ox + w * M, oy + 11 * M, Wall, 2);

        // Türöffnungen als Lücke mit Türblatt
        Door(c, ox + 4 * M, oy + 6 * M);
        Door(c, ox + 14 * M, oy + 6 * M);
        Door(c, ox + 24 * M, oy + 6 * M);

        // Räume (Achsmaße, ohne Wandabzug)
        Room(c, ox + 2 * M, oy + 11 * M, "0.01 Wohnen", "100,00 m²");
        Room(c, ox + 12 * M, oy + 11 * M, "0.02 Küche", "100,00 m²");
        Room(c, ox + 22 * M, oy + 13 * M, "0.03 Bad", "50,00 m²");
        Room(c, ox + 22 * M, oy + 8 * M, "0.04 Abstellraum", "50,00 m²");
        Room(c, ox + 2 * M, oy + 3 * M, "0.05 Zimmer 1", "60,00 m²");
        Room(c, ox + 12 * M, oy + 3 * M, "0.06 Zimmer 2", "60,00 m²");
        Room(c, ox + 22 * M, oy + 3 * M, "0.07 Flur", "60,00 m²");

        // Bemaßung unten
        var dy = oy - 130;
        c.Line(ox, dy, ox + w * M, dy, Dimension, 0.8);
        for (var i = 0; i <= 3; i++)
        {
            var x = ox + i * 10 * M;
            c.Line(x, dy - 8, x, dy + 8, Dimension, 0.8);
            if (i < 3)
            {
                c.Text(x + 5 * M - 20, dy + 8, "10,00", 14, Dimension);
            }
        }
        c.Text(ox + 15 * M - 22, dy - 26, "30,00", 14, Dimension);

        // Plankopf
        var bx = pageW - 28 - 560;
        const double by = 28, bw = 560, bh = 200;
        c.Rectangle(bx, by, bw, bh, Wall, null, 1.5);
        c.Line(bx, by + 140, bx + bw, by + 140, Wall, 0.8);
        c.Line(bx, by + 90, bx + bw, by + 90, Wall, 0.8);
        c.Line(bx, by + 45, bx + bw, by + 45, Wall, 0.8);
        c.Line(bx + 300, by, bx + 300, by + 90, Wall, 0.8);
        c.Text(bx + 12, by + 165, "Beispielprojekt Wohnanlage (Testdaten)", 18, Wall);
        c.Text(bx + 12, by + 110, "Grundriss Erdgeschoss", 22, Wall);
        c.Text(bx + 12, by + 62, "Plannummer GR-EG-01", 14, Wall);
        c.Text(bx + 312, by + 62, "Index A", 14, Wall);
        c.Text(bx + 12, by + 17, "Maßstab 1:50", 14, Wall);
        c.Text(bx + 312, by + 17, "Format A1", 14, Wall);
    }

    /// <summary>Prüfvermerke auf dem Grundriss, wie sie bei einer Planprüfung entstehen (erfundene Inhalte).</summary>
    private static List<Markup> Markups(double pageW)
    {
        const double ox = 220, oy = 360;
        var red = new PdfColor(200, 30, 30);
        var blue = new PdfColor(22, 119, 210);
        var created = new DateTimeOffset(2026, 10, 16, 9, 30, 0, TimeSpan.FromHours(2));
        Markup Make(MarkupKind kind, MarkupStyle style, string text, params PdfPoint[] pts) =>
            new($"e360-beispiel-{kind.ToString().ToLowerInvariant()}", kind, [pts], style, text, "Planprüfung", created, created);

        var bx = pageW - 28 - 560;
        return
        [
            Make(MarkupKind.Cloud, new MarkupStyle(red, null, 1, 2), "Lüftung Bad fehlt",
                new(ox + 20.4 * M, oy + 11.4 * M), new(ox + 29.6 * M, oy + 11.4 * M), new(ox + 29.6 * M, oy + 15.6 * M), new(ox + 20.4 * M, oy + 15.6 * M)),
            Make(MarkupKind.Arrow, new MarkupStyle(red, null, 1, 2), "", new(2030, 1195), new(ox + 29.9 * M, oy + 14 * M)),
            Make(MarkupKind.TextBox, new MarkupStyle(red, PdfColor.White, 1, 1.5, 16), "Lüftung Bad fehlt.\nAbluft nach DIN 18017-3 ergänzen.",
                new(2030, 1120), new(2340, 1230)),
            Make(MarkupKind.Rectangle, new MarkupStyle(blue, new PdfColor(209, 228, 246), 0.75, 1.5), "Türbreite prüfen",
                new(ox + 13.7 * M, oy + 5.5 * M), new(ox + 15.3 * M, oy + 7.5 * M)),
            Make(MarkupKind.Note, MarkupStyle.DefaultFor(MarkupKind.Note), "Türbreite 1,01 m lichte Breite bestätigen.", new PdfPoint(ox + 15.5 * M, oy + 8.2 * M)),
            Make(MarkupKind.Highlight, MarkupStyle.DefaultFor(MarkupKind.Highlight), "", new(bx + 8, 28 + 56), new(bx + 200, 28 + 82)),
            Make(MarkupKind.Polyline, new MarkupStyle(new PdfColor(30, 140, 60), null, 1, 2), "Fluchtweg",
                new(ox + 5 * M, oy + 3 * M), new(ox + 5 * M, oy + 6.5 * M), new(ox + 25 * M, oy + 6.5 * M), new(ox + 25 * M, oy + 9 * M)),
        ];
    }

    /// <summary>Beispielmessungen im Maßstab 1:50: Fläche der Küche, Gebäudeabschnitt, Anzahl der Innentüren.</summary>
    private static List<Markup> Measurements()
    {
        const double ox = 220, oy = 360;
        var scale = MeasureScale.FromRatio(50);
        var created = new DateTimeOffset(2026, 10, 16, 9, 45, 0, TimeSpan.FromHours(2));
        Markup Make(MarkupKind kind, string text, params PdfPoint[] pts) =>
            new($"e360-beispiel-{kind.ToString().ToLowerInvariant()}", kind, [pts], MarkupStyle.DefaultFor(kind), text, "Mengenermittlung", created, created,
                Scale: MarkupKinds.NeedsScale(kind) ? scale : null);
        return
        [
            Make(MarkupKind.Area, "0.02 Küche", new(ox + 10 * M, oy + 6 * M), new(ox + 20 * M, oy + 6 * M), new(ox + 20 * M, oy + 16 * M), new(ox + 10 * M, oy + 16 * M)),
            Make(MarkupKind.Length, "Achse A bis C", new(ox, oy + 17 * M), new(ox + 10 * M, oy + 17 * M)),
            Make(MarkupKind.Count, "Innentüren", new(ox + 4.5 * M, oy + 6 * M), new(ox + 14.5 * M, oy + 6 * M), new(ox + 24.5 * M, oy + 6 * M)),
        ];
    }

    private static void Door(PdfContentWriter c, double x, double y)
    {
        c.Rectangle(x, y - 3, 1.0 * M, 6, null, PdfColor.White);
        c.Line(x, y, x + 0.95 * M, y + 0.95 * M, Wall, 1);
    }

    private static void Room(PdfContentWriter c, double x, double y, string name, string area)
    {
        c.Text(x, y, name, 18, Wall);
        c.Text(x, y - 24, area, 14, new PdfColor(90, 90, 90));
    }

    private static void Section(PdfContentWriter c, double pageW, double pageH)
    {
        c.Rectangle(20, 20, pageW - 40, pageH - 40, Wall, null, 1.2);
        const double s = 72 / 25.4 * 1000 / 100; // 1:100
        const double ox = 120, oy = 160;
        c.Line(ox - 40, oy, ox + 30 * s + 40, oy, Wall, 2.5);
        c.Rectangle(ox, oy, 30 * s, 3 * s, Wall, null, 2);
        c.Rectangle(ox, oy + 3 * s, 30 * s, 3 * s, Wall, null, 2);
        c.Polyline([(ox - 10, oy + 6 * s), (ox + 15 * s, oy + 9 * s), (ox + 30 * s + 10, oy + 6 * s)], Wall, 2);
        c.Text(ox + 30 * s + 20, oy + 3 * s - 5, "+3,00", 12, Dimension);
        c.Text(ox + 30 * s + 20, oy + 6 * s - 5, "+6,00", 12, Dimension);
        c.Text(ox + 30 * s + 20, oy - 5, "±0,00", 12, Dimension);
        c.Text(40, pageH - 60, "Schnitt A-A, Maßstab 1:100 (Testdaten)", 18, Wall);
    }
}
