using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Egle360.Pdf;

namespace Egle360.Desktop.Services;

/// <summary>
/// Drucken über den Windows-Druckdialog. Jede Seite wird als Bild in Druckauflösung gerendert und auf den
/// bedruckbaren Bereich eingepasst; quer liegende Pläne werden auf Hochformat-Papier gedreht.
/// </summary>
internal static class PdfPrinter
{
    /// <summary>Druckauflösung der Seitenbilder in dpi; reicht für Pläne und hält den Speicherbedarf in Grenzen.</summary>
    private const double Dpi = 200;

    public static void Print(PdfDocument document, string title, int currentPage)
    {
        var dialog = new PrintDialog
        {
            UserPageRangeEnabled = true,
            CurrentPageEnabled = true,
            MinPage = 1,
            MaxPage = (uint)document.PageCount,
            PageRange = new System.Windows.Controls.PageRange(1, document.PageCount),
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }
        var pages = dialog.PageRangeSelection switch
        {
            PageRangeSelection.CurrentPage => [Math.Clamp(currentPage, 0, document.PageCount - 1)],
            PageRangeSelection.UserPages => Enumerable.Range(dialog.PageRange.PageFrom - 1, dialog.PageRange.PageTo - dialog.PageRange.PageFrom + 1)
                .Where(i => i >= 0 && i < document.PageCount).ToList(),
            _ => Enumerable.Range(0, document.PageCount).ToList(),
        };
        if (pages.Count == 0)
        {
            return;
        }
        var area = new Size(dialog.PrintableAreaWidth, dialog.PrintableAreaHeight);
        dialog.PrintDocument(CreatePaginator(document, pages, area), title);
    }

    /// <summary>Seitenfolge für den Druck; auch für den Rauchtest ohne Drucker (Ausgabe als XPS).</summary>
    public static DocumentPaginator CreatePaginator(PdfDocument document, IReadOnlyList<int> pages, Size area) => new Paginator(document, pages, area);

    private sealed class Paginator(PdfDocument document, IReadOnlyList<int> pages, Size area) : DocumentPaginator
    {
        public override bool IsPageCountValid => true;

        public override int PageCount => pages.Count;

        public override Size PageSize { get; set; } = area;

        public override IDocumentPaginatorSource? Source => null;

        public override DocumentPage GetPage(int pageNumber)
        {
            var index = pages[pageNumber];
            var info = document.GetPageInfo(index);
            double w = info.WidthPt, h = info.HeightPt;
            var paper = PageSize;
            // Querformat auf Hochformat-Papier (und umgekehrt) um 90 Grad drehen, damit der Plan größer gedruckt wird.
            var rotate = (w > h) != (paper.Width > paper.Height);
            var fitW = rotate ? h : w;
            var fitH = rotate ? w : h;
            var dipPerPt = Math.Min(paper.Width / fitW, paper.Height / fitH);
            var pxPerPt = Math.Min(dipPerPt * Dpi / 96, Math.Sqrt(80_000_000 / (w * h)));
            var (pw, ph) = document.PagePixelSize(index, pxPerPt);
            var buffer = new byte[pw * ph * 4];
            document.Render(index, pxPerPt, new PixelRect(0, 0, pw, ph), buffer, pw * 4, annotations: true, forPrinting: true);
            var bitmap = BitmapSource.Create(pw, ph, 96, 96, PixelFormats.Pbgra32, null, buffer, pw * 4);
            bitmap.Freeze();

            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen())
            {
                var drawW = w * dipPerPt;
                var drawH = h * dipPerPt;
                dc.PushTransform(new TranslateTransform(paper.Width / 2, paper.Height / 2));
                if (rotate)
                {
                    dc.PushTransform(new RotateTransform(90));
                }
                dc.DrawImage(bitmap, new Rect(-drawW / 2, -drawH / 2, drawW, drawH));
            }
            var box = new Rect(paper);
            return new DocumentPage(visual, paper, box, box);
        }
    }
}
