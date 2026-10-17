using Egle360.Pdf.Markups;
using Egle360.Pdf.Viewing;

namespace Egle360.Pdf.Compare;

/// <summary>
/// Ergebnis eines Seitenvergleichs: ein Überlagerungsbild (BGRA, Zeilen von oben nach unten) und die geänderten
/// Bereiche in Benutzerkoordinaten der neuen Seite.
/// </summary>
/// <param name="Scale">Pixel je Punkt des Bildes.</param>
public sealed record PageComparison(int Width, int Height, double Scale, byte[] Overlay, IReadOnlyList<PdfRect> Changes, long ChangedPixels)
{
    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// Vergleicht zwei Planstände pixelweise. Beide Seiten werden ohne Anmerkungen gerendert und an der linken oberen Ecke
/// übereinandergelegt; eine Verschiebung des Planinhalts zwischen den Ständen wird nicht ausgeglichen.
/// In der Überlagerung erscheint nur im alten Stand Vorhandenes rot, nur im neuen Stand Vorhandenes blau und
/// Unverändertes grau.
/// </summary>
public static class PageComparer
{
    public static readonly PdfColor OldOnly = new(220, 40, 40);
    public static readonly PdfColor NewOnly = new(20, 90, 230);
    public static readonly PdfColor Same = new(150, 150, 150);

    /// <summary>Raster in Pixeln, in dem Unterschiede gesammelt werden.</summary>
    private const int Cell = 8;

    /// <summary>So viele abweichende Pixel braucht eine Zelle, damit sie als geändert gilt (gegen Kantenglättung).</summary>
    private const int CellThreshold = 4;

    /// <summary>Höchstzahl gemeldeter Bereiche.</summary>
    public const int MaxChanges = 300;

    /// <summary>Maßstab, bei dem die größere der beiden Seiten etwa <paramref name="maxPixels"/> Pixel hat, höchstens 2.</summary>
    public static double ScaleFor(PdfDocument a, int pageA, PdfDocument b, int pageB, double maxPixels = 24e6)
    {
        var (wa, ha) = a.PagePixelSize(pageA, 1);
        var (wb, hb) = b.PagePixelSize(pageB, 1);
        var area = Math.Max((double)Math.Max(wa, wb) * Math.Max(ha, hb), 1);
        return Math.Clamp(Math.Sqrt(maxPixels / area), 0.25, 2);
    }

    public static PageComparison Compare(PdfDocument oldDoc, int oldPage, PdfDocument newDoc, int newPage, double scale)
    {
        var (wo, ho) = oldDoc.PagePixelSize(oldPage, scale);
        var (wn, hn) = newDoc.PagePixelSize(newPage, scale);
        var w = Math.Max(wo, wn);
        var h = Math.Max(ho, hn);
        var a = Render(oldDoc, oldPage, scale, wo, ho);
        var b = Render(newDoc, newPage, scale, wn, hn);

        var overlay = new byte[w * h * 4];
        var cols = (w + Cell - 1) / Cell;
        var rows = (h + Cell - 1) / Cell;
        var cellCounts = new int[cols * rows];
        long changed = 0;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                var inkA = x < wo && y < ho && IsInk(a, (y * wo + x) * 4);
                var inkB = x < wn && y < hn && IsInk(b, (y * wn + x) * 4);
                var c = (inkA, inkB) switch
                {
                    (true, true) => Same,
                    (true, false) => OldOnly,
                    (false, true) => NewOnly,
                    _ => PdfColor.White,
                };
                var i = (y * w + x) * 4;
                overlay[i] = c.B;
                overlay[i + 1] = c.G;
                overlay[i + 2] = c.R;
                overlay[i + 3] = 255;
                if (inkA != inkB)
                {
                    changed++;
                    cellCounts[y / Cell * cols + x / Cell]++;
                }
            }
        }

        var boxes = Regions(cellCounts, cols, rows);
        var transform = newDoc.GetPageTransform(newPage);
        var changes = boxes
            .Select(r => ToUser(transform, r, scale, w, h))
            .Take(MaxChanges)
            .ToList();
        return new PageComparison(w, h, scale, overlay, changes, changed);
    }

    private static byte[] Render(PdfDocument doc, int page, double scale, int w, int h)
    {
        var buffer = new byte[w * h * 4];
        doc.Render(page, scale, new PixelRect(0, 0, w, h), buffer, w * 4, annotations: false);
        return buffer;
    }

    /// <summary>Dunkler als ein heller Grauton gilt als Planinhalt.</summary>
    private static bool IsInk(byte[] bgra, int i) => bgra[i] * 114 + bgra[i + 1] * 587 + bgra[i + 2] * 299 < 200 * 1000;

    /// <summary>Zusammenhängende geänderte Zellen (mit einer Zelle Abstand) als Rechtecke in Zellen.</summary>
    private static List<(int X0, int Y0, int X1, int Y1)> Regions(int[] counts, int cols, int rows)
    {
        var hot = new bool[counts.Length];
        for (var i = 0; i < counts.Length; i++)
        {
            hot[i] = counts[i] >= CellThreshold;
        }
        var seen = new bool[counts.Length];
        var result = new List<(int, int, int, int)>();
        var stack = new Stack<int>();
        for (var start = 0; start < hot.Length; start++)
        {
            if (!hot[start] || seen[start])
            {
                continue;
            }
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = -1, y1 = -1;
            stack.Push(start);
            seen[start] = true;
            while (stack.Count > 0)
            {
                var k = stack.Pop();
                int cx = k % cols, cy = k / cols;
                x0 = Math.Min(x0, cx);
                y0 = Math.Min(y0, cy);
                x1 = Math.Max(x1, cx);
                y1 = Math.Max(y1, cy);
                // Nachbarn bis zwei Zellen Abstand gehören zum selben Bereich, damit eine Änderung nicht in Splitter zerfällt.
                for (var dy = -2; dy <= 2; dy++)
                {
                    for (var dx = -2; dx <= 2; dx++)
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= cols || ny >= rows)
                        {
                            continue;
                        }
                        var n = ny * cols + nx;
                        if (hot[n] && !seen[n])
                        {
                            seen[n] = true;
                            stack.Push(n);
                        }
                    }
                }
            }
            result.Add((x0, y0, x1, y1));
        }
        return Merge(result);
    }

    /// <summary>Überlappende Rechtecke zusammenfassen, bis keine mehr überlappen.</summary>
    private static List<(int X0, int Y0, int X1, int Y1)> Merge(List<(int X0, int Y0, int X1, int Y1)> boxes)
    {
        var merged = true;
        while (merged)
        {
            merged = false;
            for (var i = 0; i < boxes.Count && !merged; i++)
            {
                for (var j = i + 1; j < boxes.Count; j++)
                {
                    var (a, b) = (boxes[i], boxes[j]);
                    if (a.X0 <= b.X1 + 1 && b.X0 <= a.X1 + 1 && a.Y0 <= b.Y1 + 1 && b.Y0 <= a.Y1 + 1)
                    {
                        boxes[i] = (Math.Min(a.X0, b.X0), Math.Min(a.Y0, b.Y0), Math.Max(a.X1, b.X1), Math.Max(a.Y1, b.Y1));
                        boxes.RemoveAt(j);
                        merged = true;
                        break;
                    }
                }
            }
        }
        // Große Bereiche zuerst, damit bei sehr vielen Änderungen die wichtigsten gemeldet werden.
        return [.. boxes.OrderByDescending(b => (b.X1 - b.X0 + 1) * (b.Y1 - b.Y0 + 1))];
    }

    private static PdfRect ToUser(PageTransform t, (int X0, int Y0, int X1, int Y1) cells, double scale, int w, int h)
    {
        // Etwas Rand, damit die Wolke die Änderung umschließt statt sie zu berühren.
        const int pad = 6;
        var u0 = Math.Max(0, cells.X0 * Cell - pad) / scale;
        var v0 = Math.Max(0, cells.Y0 * Cell - pad) / scale;
        var u1 = Math.Min(w, (cells.X1 + 1) * Cell + pad) / scale;
        var v1 = Math.Min(h, (cells.Y1 + 1) * Cell + pad) / scale;
        var p = t.ToUser(u0, v0);
        var q = t.ToUser(u1, v1);
        return PdfRect.FromPoints(p, q);
    }

    /// <summary>Wolken um die geänderten Bereiche, zum Eintragen in den neuen Stand.</summary>
    public static IReadOnlyList<Markup> ChangeClouds(IEnumerable<PdfRect> changes, string author, string text) =>
        [.. changes.Select(r => Markup.Create(MarkupKind.Cloud,
            [[new PdfPoint(r.Left, r.Bottom), new PdfPoint(r.Right, r.Bottom), new PdfPoint(r.Right, r.Top), new PdfPoint(r.Left, r.Top)]],
            new MarkupStyle(new PdfColor(220, 40, 40), null, 1, 1.5), author, text: text))];
}
