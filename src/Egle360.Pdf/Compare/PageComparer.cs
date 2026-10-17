using Egle360.Pdf.Markups;
using Egle360.Pdf.Viewing;

namespace Egle360.Pdf.Compare;

/// <summary>
/// Ergebnis eines Seitenvergleichs: ein Überlagerungsbild (BGRA, Zeilen von oben nach unten) und die geänderten
/// Bereiche in Benutzerkoordinaten der neuen Seite.
/// </summary>
/// <param name="Scale">Pixel je Punkt des Bildes.</param>
/// <param name="OffsetX">Ausgeglichene Verschiebung des neuen Stands gegenüber dem alten in Pixeln (nach rechts positiv).</param>
/// <param name="OffsetY">Ausgeglichene Verschiebung in Pixeln (nach unten positiv).</param>
public sealed record PageComparison(int Width, int Height, double Scale, byte[] Overlay, IReadOnlyList<PdfRect> Changes, long ChangedPixels,
    int OffsetX = 0, int OffsetY = 0)
{
    /// <summary>Verschiebung auf dem Papier in Millimetern (x nach rechts, y nach unten).</summary>
    public (double X, double Y) OffsetMillimeters => (OffsetX / Scale * 25.4 / 72, OffsetY / Scale * 25.4 / 72);

    public bool HasChanges => Changes.Count > 0;
}

/// <summary>
/// Vergleicht zwei Planstände pixelweise. Beide Seiten werden ohne Anmerkungen gerendert und übereinandergelegt. Auf
/// Wunsch wird eine Verschiebung des Planinhalts zwischen den Ständen gesucht und ausgeglichen (nur Verschiebung, keine
/// Drehung und kein anderer Maßstab).
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
    public static double ScaleFor(PdfDocument a, int pageA, PdfDocument b, int pageB, double maxPixels = 16e6)
    {
        var (wa, ha) = a.PagePixelSize(pageA, 1);
        var (wb, hb) = b.PagePixelSize(pageB, 1);
        var area = Math.Max((double)Math.Max(wa, wb) * Math.Max(ha, hb), 1);
        return Math.Clamp(Math.Sqrt(maxPixels / area), 0.25, 2);
    }

    public static PageComparison Compare(PdfDocument oldDoc, int oldPage, PdfDocument newDoc, int newPage, double scale, bool align = false)
    {
        var (wo, ho) = oldDoc.PagePixelSize(oldPage, scale);
        var (wn, hn) = newDoc.PagePixelSize(newPage, scale);
        var w = Math.Max(wo, wn);
        var h = Math.Max(ho, hn);
        var imageA = Render(oldDoc, oldPage, scale, wo, ho);
        var imageB = Render(newDoc, newPage, scale, wn, hn);
        var a = Ink(imageA);
        var b = Ink(imageB);
        // Ein Pixel Spielraum: Linien, die nur um Bruchteile eines Pixels versetzt oder anders geglättet sind, gelten
        // nicht als Änderung.
        var nearA = Dilate(a, wo, ho);
        var nearB = Dilate(b, wn, hn);
        var (dx, dy) = align ? FindOffset(imageA, a, nearA, wo, ho, imageB, b, nearB, wn, hn) : (0, 0);

        var overlay = new byte[w * h * 4];
        var cols = (w + Cell - 1) / Cell;
        var rows = (h + Cell - 1) / Cell;
        var cellCounts = new int[cols * rows];
        long changed = 0;
        // Je Band von Cell Pixelzeilen parallel; jedes Band schreibt nur in seine eigene Zeile der Zellzählung.
        Parallel.For(0, rows, band =>
        {
            long local = 0;
            for (var y = band * Cell; y < Math.Min(h, (band + 1) * Cell); y++)
            {
                for (var x = 0; x < w; x++)
                {
                    var inA = x < wo && y < ho;
                    int bx = x + dx, by = y + dy;
                    var inB = bx >= 0 && by >= 0 && bx < wn && by < hn;
                    var inkA = inA && a[y * wo + x];
                    var inkB = inB && b[by * wn + bx];
                    var oldOnly = inkA && !(inB && nearB[by * wn + bx]);
                    var newOnly = inkB && !(inA && nearA[y * wo + x]);
                    var c = oldOnly ? OldOnly : newOnly ? NewOnly : inkA || inkB ? Same : PdfColor.White;
                    var i = (y * w + x) * 4;
                    overlay[i] = c.B;
                    overlay[i + 1] = c.G;
                    overlay[i + 2] = c.R;
                    overlay[i + 3] = 255;
                    if (oldOnly || newOnly)
                    {
                        local++;
                        cellCounts[band * cols + x / Cell]++;
                    }
                }
            }
            Interlocked.Add(ref changed, local);
        });

        var boxes = Regions(cellCounts, cols, rows);
        var transform = newDoc.GetPageTransform(newPage);
        var changes = boxes
            .Select(r => ToUser(transform, r, scale, w, h, dx, dy))
            .Take(MaxChanges)
            .ToList();
        return new PageComparison(w, h, scale, overlay, changes, changed, dx, dy);
    }

    /// <summary>
    /// Kopie der Überlagerung, in der ausgeblendete Kategorien weiß sind: nur im alten Stand (rot), nur im neuen
    /// (blau), unverändert (grau).
    /// </summary>
    public static byte[] Filter(byte[] overlay, bool showOld, bool showNew, bool showSame)
    {
        var result = (byte[])overlay.Clone();
        if (showOld && showNew && showSame)
        {
            return result;
        }
        static bool Is(byte[] o, int i, PdfColor c) => o[i] == c.B && o[i + 1] == c.G && o[i + 2] == c.R;
        var pixels = result.Length / 4;
        Parallel.For(0, (pixels + 65535) / 65536, chunk =>
        {
            for (var p = chunk * 65536; p < Math.Min(pixels, (chunk + 1) * 65536); p++)
            {
                var i = p * 4;
                if ((!showOld && Is(result, i, OldOnly)) || (!showNew && Is(result, i, NewOnly)) || (!showSame && Is(result, i, Same)))
                {
                    result[i] = result[i + 1] = result[i + 2] = 255;
                }
            }
        });
        return result;
    }

    private static byte[] Render(PdfDocument doc, int page, double scale, int w, int h)
    {
        var buffer = new byte[w * h * 4];
        doc.Render(page, scale, new PixelRect(0, 0, w, h), buffer, w * 4, annotations: false);
        return buffer;
    }

    /// <summary>Dunkler als ein heller Grauton gilt als Planinhalt.</summary>
    private static bool[] Ink(byte[] bgra)
    {
        var ink = new bool[bgra.Length / 4];
        Parallel.For(0, (ink.Length + 65535) / 65536, chunk =>
        {
            for (var i = chunk * 65536; i < Math.Min(ink.Length, (chunk + 1) * 65536); i++)
            {
                var j = i * 4;
                ink[i] = bgra[j] * 114 + bgra[j + 1] * 587 + bgra[j + 2] * 299 < 200 * 1000;
            }
        });
        return ink;
    }

    /// <summary>
    /// Sucht die Verschiebung (dx, dy), bei der der neue Stand am besten auf dem alten liegt: neuer Pixel (x+dx, y+dy)
    /// entspricht altem Pixel (x, y). Zuerst getrennt für x und y über die geglätteten Spalten- und Zeilensummen der
    /// Schwärzung; davon die besten Kandidaten je Richtung. Bei regelmäßigen Rastern passt eine um eine Rasterweite
    /// versetzte Lage in einer Richtung oft fast genauso gut, deshalb entscheidet ein Vergleich in der Fläche zwischen
    /// den Kandidaten, danach eine Feinsuche um einen Pixel. Gesucht wird bis etwa 6 % der Seitengröße.
    /// </summary>
    private static (int Dx, int Dy) FindOffset(
        byte[] imageA, bool[] a, bool[] nearA, int wa, int ha, byte[] imageB, bool[] b, bool[] nearB, int wb, int hb)
    {
        const int candidates = 5;
        var reach = (int)(Math.Max(Math.Max(wa, ha), Math.Max(wb, hb)) * 0.06);
        var (colsA, rowsA) = Profiles(imageA, wa, ha);
        var (colsB, rowsB) = Profiles(imageB, wb, hb);
        var xs = BestShifts(Smooth(colsA), Smooth(colsB), reach, candidates);
        var ys = BestShifts(Smooth(rowsA), Smooth(rowsB), reach, candidates);

        long Score(int sx, int sy, int step, bool tolerant)
        {
            long total = 0;
            Parallel.For(0, (ha + step - 1) / step, () => 0L, (iy, _, local) =>
            {
                var y = iy * step;
                var yb = y + sy;
                for (var x = 0; x < wa; x += step)
                {
                    var xb = x + sx;
                    var inB = yb >= 0 && xb >= 0 && yb < hb && xb < wb;
                    var ia = y * wa + x;
                    var inkB = inB && b[yb * wb + xb];
                    if (tolerant)
                    {
                        if (a[ia] && !(inB && nearB[yb * wb + xb]))
                        {
                            local++;
                        }
                        if (inkB && !nearA[ia])
                        {
                            local++;
                        }
                    }
                    else if (a[ia] != inkB)
                    {
                        local++;
                    }
                }
                return local;
            }, local => Interlocked.Add(ref total, local));
            return total;
        }

        (int, int) Pick(IEnumerable<(int X, int Y)> options, int step, bool tolerant)
        {
            long best = long.MaxValue;
            var result = (0, 0);
            foreach (var (sx, sy) in options)
            {
                var score = Score(sx, sy, step, tolerant);
                if (score < best || (score == best && Math.Abs(sx) + Math.Abs(sy) < Math.Abs(result.Item1) + Math.Abs(result.Item2)))
                {
                    best = score;
                    result = (sx, sy);
                }
            }
            return result;
        }

        // Grob über alle Kandidatenpaare mit einem Pixel Spielraum (Versatz um Bruchteile eines Pixels zählt nicht),
        // dann pixelgenau um das beste Paar.
        var (dx, dy) = Pick(xs.SelectMany(x => ys.Select(y => (x, y))), 4, tolerant: true);
        return Pick(
            from sy in Enumerable.Range(dy - 1, 3)
            from sx in Enumerable.Range(dx - 1, 3)
            select (sx, sy), 2, tolerant: false);
    }

    /// <summary>Planinhalt um einen Pixel in alle Richtungen erweitert (3 × 3).</summary>
    private static bool[] Dilate(bool[] ink, int w, int h)
    {
        var horizontal = new bool[ink.Length];
        Parallel.For(0, h, y =>
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                horizontal[row + x] = ink[row + x] || (x > 0 && ink[row + x - 1]) || (x < w - 1 && ink[row + x + 1]);
            }
        });
        var result = new bool[ink.Length];
        Parallel.For(0, h, y =>
        {
            var row = y * w;
            for (var x = 0; x < w; x++)
            {
                result[row + x] = horizontal[row + x] || (y > 0 && horizontal[row - w + x]) || (y < h - 1 && horizontal[row + w + x]);
            }
        });
        return result;
    }

    /// <summary>Summe der Schwärzung (255 minus Helligkeit) je Spalte und je Zeile.</summary>
    private static (long[] Cols, long[] Rows) Profiles(byte[] bgra, int w, int h)
    {
        var cols = new long[w];
        var rows = new long[h];
        for (var y = 0; y < h; y++)
        {
            var row = y * w;
            long sum = 0;
            for (var x = 0; x < w; x++)
            {
                var j = (row + x) * 4;
                var dark = 255 - (bgra[j] * 114 + bgra[j + 1] * 587 + bgra[j + 2] * 299) / 1000;
                cols[x] += dark;
                sum += dark;
            }
            rows[y] = sum;
        }
        return (cols, rows);
    }

    /// <summary>Gleitende Summe über 9 Werte.</summary>
    private static long[] Smooth(long[] v)
    {
        const int r = 4;
        var result = new long[v.Length];
        long sum = 0;
        for (var i = -r; i < v.Length; i++)
        {
            if (i + r < v.Length)
            {
                sum += v[i + r];
            }
            if (i - r - 1 >= 0)
            {
                sum -= v[i - r - 1];
            }
            if (i >= 0)
            {
                result[i] = sum;
            }
        }
        return result;
    }

    /// <summary>
    /// Die <paramref name="count"/> Verschiebungen d mit der kleinsten Abweichung von b[i + d] zu a[i], nur lokale
    /// Minima (Nachbarn einer guten Lage wären sonst eigene Kandidaten). Die Lage 0 ist immer dabei.
    /// </summary>
    private static List<int> BestShifts(long[] a, long[] b, int reach, int count)
    {
        var scores = new long[2 * reach + 1];
        Parallel.For(-reach, reach + 1, d =>
        {
            long score = 0;
            for (var i = 0; i < a.Length; i++)
            {
                var j = i + d;
                score += Math.Abs(a[i] - (j >= 0 && j < b.Length ? b[j] : 0));
            }
            scores[d + reach] = score;
        });
        var minima = new List<int>();
        for (var k = 0; k < scores.Length; k++)
        {
            if ((k == 0 || scores[k] <= scores[k - 1]) && (k == scores.Length - 1 || scores[k] <= scores[k + 1]))
            {
                minima.Add(k - reach);
            }
        }
        var result = minima.OrderBy(d => scores[d + reach]).ThenBy(Math.Abs).Take(count).ToList();
        if (!result.Contains(0))
        {
            result.Add(0);
        }
        return result;
    }

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

    private static PdfRect ToUser(PageTransform t, (int X0, int Y0, int X1, int Y1) cells, double scale, int w, int h, int dx, int dy)
    {
        // Etwas Rand, damit die Wolke die Änderung umschließt statt sie zu berühren. Die Zellen liegen im Raster des
        // alten Stands; die Wolke gehört in den neuen, also um die Verschiebung versetzt.
        const int pad = 6;
        var u0 = (Math.Max(0, cells.X0 * Cell - pad) + dx) / scale;
        var v0 = (Math.Max(0, cells.Y0 * Cell - pad) + dy) / scale;
        var u1 = (Math.Min(w, (cells.X1 + 1) * Cell + pad) + dx) / scale;
        var v1 = (Math.Min(h, (cells.Y1 + 1) * Cell + pad) + dy) / scale;
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
