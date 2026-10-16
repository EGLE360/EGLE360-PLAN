using Egle360.Pdf.Viewing;

namespace Egle360.Pdf.Tests;

public class ViewingTests
{
    private static readonly (double, double) A4 = (595.28, 841.89);
    private static readonly (double, double) A1Landscape = (2383.94, 1683.78);

    private static PageLayout Layout(params (double, double)[] pages)
    {
        var layout = new PageLayout(gap: 16, margin: 24);
        layout.SetPages(pages);
        return layout;
    }

    [Fact]
    public void HundredPercentIsRealSize()
    {
        var layout = Layout(A4);
        // A4 ist 210 mm breit = 793,7 DIP bei 96 DIP je Zoll.
        Assert.Equal(210 / 25.4 * 96, layout.PageRect(0).Width, 1);
    }

    [Fact]
    public void PagesAreStackedAndCentered()
    {
        var layout = Layout(A4, A1Landscape, A4);
        var r0 = layout.PageRect(0);
        var r1 = layout.PageRect(1);
        var r2 = layout.PageRect(2);
        Assert.Equal(16, r0.Y);
        Assert.Equal(r0.Bottom + 16, r1.Y, 6);
        Assert.Equal(r1.Bottom + 16, r2.Y, 6);
        Assert.Equal(24, r1.X, 6); // breiteste Seite mit Rand
        Assert.Equal(r1.X + r1.Width / 2, r0.X + r0.Width / 2, 6);
        Assert.Equal(r2.Bottom + 16, layout.ExtentHeight, 6);
        Assert.Equal(r1.Width + 48, layout.ExtentWidth, 6);
    }

    [Fact]
    public void NarrowContentIsCenteredInViewport()
    {
        var layout = Layout(A4);
        layout.SetZoom(0.5);
        layout.SetViewportWidth(2000);
        Assert.Equal(2000, layout.ExtentWidth);
        Assert.Equal(1000, layout.PageRect(0).X + layout.PageRect(0).Width / 2, 6);
    }

    [Fact]
    public void VisiblePagesAndCurrentPage()
    {
        var layout = Layout(A4, A4, A4, A4, A4);
        var h = layout.PageRect(0).Height;
        var viewport = new LayoutRect(0, layout.PageRect(2).Y + 10, 1000, h / 2);
        Assert.Equal([2], layout.VisiblePages(viewport));
        Assert.Equal(2, layout.CurrentPage(viewport));

        var between = new LayoutRect(0, layout.PageRect(1).Bottom - 100, 1000, 300);
        Assert.Equal([1, 2], layout.VisiblePages(between));
        Assert.Equal([0, 1, 2, 3, 4], layout.VisiblePages(new LayoutRect(0, 0, 1000, layout.ExtentHeight)));
        Assert.Empty(layout.VisiblePages(new LayoutRect(0, layout.ExtentHeight + 10, 1000, 100)));
    }

    [Fact]
    public void FitWidthAndFitPage()
    {
        var layout = Layout(A4, A1Landscape);
        var fitWidth = layout.FitWidthZoom(1048);
        layout.SetZoom(fitWidth);
        Assert.Equal(1000, layout.PageRect(1).Width, 3);

        var fitPage = layout.FitPageZoom(0, 1048, 832);
        layout.SetZoom(fitPage);
        Assert.Equal(800, layout.PageRect(0).Height, 3);
        Assert.True(layout.PageRect(0).Width <= 1000);
    }

    [Theory]
    [InlineData(1.0, 1.25, 0.75)]
    [InlineData(0.9, 1.0, 0.75)]
    [InlineData(64, 64, 48)]
    [InlineData(0.02, 0.03, 0.02)]
    public void ZoomSteps(double zoom, double next, double previous)
    {
        Assert.Equal(next, PageLayout.NextZoomStep(zoom));
        Assert.Equal(previous, PageLayout.PreviousZoomStep(zoom));
    }

    [Fact]
    public void ZoomIsClamped()
    {
        var layout = Layout(A4);
        layout.SetZoom(1000);
        Assert.Equal(PageLayout.MaxZoom, layout.Zoom);
        layout.SetZoom(0);
        Assert.Equal(PageLayout.MinZoom, layout.Zoom);
    }

    [Fact]
    public void ZoomAroundKeepsAnchorInPlace()
    {
        var layout = Layout(A4, A4, A4);
        layout.SetViewportWidth(1200);
        var viewport = new LayoutRect(0, layout.PageRect(1).Y - 50, 1200, 800);
        var anchor = (X: 700.0, Y: 300.0);
        var before = layout.HitTest(viewport.X + anchor.X, viewport.Y + anchor.Y)!.Value;

        var (ox, oy) = layout.ZoomAround(3.0, viewport, anchor);
        var after = layout.HitTest(ox + anchor.X, oy + anchor.Y)!.Value;

        Assert.Equal(before.Page, after.Page);
        Assert.Equal(before.XPt, after.XPt, 6);
        Assert.Equal(before.YPt, after.YPt, 6);
    }

    [Fact]
    public void HitTestAndToLayoutAreInverse()
    {
        var layout = Layout(A4, A1Landscape);
        layout.SetZoom(0.37);
        var (x, y) = layout.ToLayout(1, 1000, 500);
        var hit = layout.HitTest(x, y)!.Value;
        Assert.Equal(1, hit.Page);
        Assert.Equal(1000, hit.XPt, 6);
        Assert.Equal(500, hit.YPt, 6);
    }

    [Fact]
    public void BaseImageIsCappedForLargePlans()
    {
        // A0 bei 400 % auf einem Bildschirm mit 150 % Skalierung.
        var required = RenderPlanner.RequiredScale(4.0, 1.5);
        Assert.Equal(8.0, required, 6);
        var baseScale = RenderPlanner.BaseScale(2383.94, 3370.39, required);
        Assert.True(2383.94 * baseScale * 3370.39 * baseScale <= RenderPlanner.MaxBasePixels * 1.0001);
        Assert.True(RenderPlanner.NeedsDetail(baseScale, required));
    }

    [Fact]
    public void SmallPagesNeedNoDetail()
    {
        var required = RenderPlanner.RequiredScale(1.0, 1.0);
        var baseScale = RenderPlanner.BaseScale(595.28, 841.89, required);
        Assert.Equal(required, baseScale);
        Assert.False(RenderPlanner.NeedsDetail(baseScale, required));
    }

    [Theory]
    [InlineData(1.0, 1.0, true)]
    [InlineData(0.96, 1.0, true)]
    [InlineData(0.9, 1.0, false)]
    [InlineData(1.5, 1.0, true)]
    [InlineData(2.0, 1.0, false)]
    public void BaseReuse(double existing, double wanted, bool expected)
    {
        Assert.Equal(expected, RenderPlanner.BaseIsGoodEnough(existing, wanted));
    }

    [Fact]
    public void DetailRegionIsRoundedOutwardAndClamped()
    {
        var region = RenderPlanner.DetailRegion(new LayoutRect(10.2, 20.7, 100.1, 50), 2.0, 150, 200);
        Assert.Equal(new PixelRect(20, 41, 130, 101), region);
    }
}
