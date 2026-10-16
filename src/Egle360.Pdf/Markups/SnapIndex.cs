namespace Egle360.Pdf.Markups;

/// <summary>Rasterindex für die schnelle Suche des nächsten Fangpunkts.</summary>
public sealed class SnapIndex
{
    private const double Cell = 24;

    private readonly Dictionary<(int, int), List<PdfPoint>> _cells = new();

    public SnapIndex(IEnumerable<PdfPoint> points)
    {
        foreach (var p in points)
        {
            if (!double.IsFinite(p.X) || !double.IsFinite(p.Y))
            {
                continue;
            }
            var key = Key(p);
            if (!_cells.TryGetValue(key, out var list))
            {
                _cells[key] = list = [];
            }
            list.Add(p);
            Count++;
        }
    }

    public static SnapIndex Empty { get; } = new([]);

    public int Count { get; }

    /// <summary>Nächster Punkt höchstens <paramref name="maxDistance"/> von <paramref name="p"/> entfernt, sonst null.</summary>
    public PdfPoint? Nearest(PdfPoint p, double maxDistance)
    {
        if (Count == 0 || maxDistance <= 0)
        {
            return null;
        }
        var reach = (int)Math.Ceiling(maxDistance / Cell);
        var (cx, cy) = Key(p);
        PdfPoint? best = null;
        var bestDistance = maxDistance;
        for (var x = cx - reach; x <= cx + reach; x++)
        {
            for (var y = cy - reach; y <= cy + reach; y++)
            {
                if (!_cells.TryGetValue((x, y), out var list))
                {
                    continue;
                }
                foreach (var q in list)
                {
                    var d = q.DistanceTo(p);
                    if (d <= bestDistance)
                    {
                        bestDistance = d;
                        best = q;
                    }
                }
            }
        }
        return best;
    }

    private static (int, int) Key(PdfPoint p) => ((int)Math.Floor(p.X / Cell), (int)Math.Floor(p.Y / Cell));
}
