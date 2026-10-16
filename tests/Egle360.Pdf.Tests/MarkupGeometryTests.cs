using Egle360.Pdf.Markups;
using Egle360.Pdf.Viewing;

namespace Egle360.Pdf.Tests;

/// <summary>Geometrie, Zeichenablauf, Daten und Verlauf der Markierungen ohne PDF-Datei.</summary>
public class MarkupGeometryTests
{
    private static readonly MarkupStyle Red = new(new PdfColor(200, 30, 30), null, 1, 2);

    private static Markup Make(MarkupKind kind, params PdfPoint[] pts) =>
        Markup.Create(kind, [pts], MarkupStyle.DefaultFor(kind) with { LineWidth = 2 }, "Test");

    [Fact]
    public void RectangleBoundsIncludeHalfLineWidthAndMargin()
    {
        var m = Make(MarkupKind.Rectangle, new(100, 100), new(200, 150));
        Assert.Equal(new PdfRect(98, 98, 202, 152), MarkupGeometry.Bounds(m));
    }

    [Fact]
    public void ArrowHeadPointsBackFromTheTip()
    {
        var head = MarkupGeometry.ArrowHead(new(0, 0), new(100, 0), 1);
        var fig = Assert.Single(head.Figures);
        Assert.Equal(new PdfPoint(100, 0), fig.Segments[0].End);
        Assert.True(fig.Start.X < 100 && fig.Start.Y > 0);
        Assert.True(fig.Segments[1].End.X < 100 && fig.Segments[1].End.Y < 0);
        Assert.Equal(10.5, 100 - fig.Start.X, 6);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CloudBulgesOutwardForBothOrientations(bool clockwise)
    {
        PdfPoint[] square = [new(0, 0), new(100, 0), new(100, 100), new(0, 100)];
        if (clockwise)
        {
            Array.Reverse(square);
        }
        var cloud = MarkupGeometry.Cloud(square, 20);
        var bounds = cloud.Bounds();
        Assert.True(bounds.Left < -5 && bounds.Bottom < -5 && bounds.Right > 105 && bounds.Top > 105);
        // 4 Kanten zu je 5 Bögen
        Assert.Equal(20, cloud.Figures[0].Segments.Count);
        Assert.True(cloud.Figures[0].Closed);
        // Die Bogenmitten liegen außerhalb des Quadrats.
        var flat = cloud.Flatten().First().Points;
        Assert.DoesNotContain(flat, p => p.X > 1 && p.X < 99 && p.Y > 1 && p.Y < 99);
    }

    [Fact]
    public void HitTestUsesLineForEmptyShapesAndAreaForFilledOnes()
    {
        var empty = Make(MarkupKind.Rectangle, new(0, 0), new(100, 100));
        Assert.True(MarkupGeometry.HitTest(empty, new(100.5, 50), 2));
        Assert.False(MarkupGeometry.HitTest(empty, new(50, 50), 2));
        var filled = empty with { Style = empty.Style with { Fill = PdfColor.White } };
        Assert.True(MarkupGeometry.HitTest(filled, new(50, 50), 2));
        var text = Make(MarkupKind.TextBox, new(0, 0), new(100, 100));
        Assert.True(MarkupGeometry.HitTest(text, new(50, 50), 2));
        var line = Make(MarkupKind.Line, new(0, 0), new(100, 100));
        Assert.True(MarkupGeometry.HitTest(line, new(50, 52), 2));
        Assert.False(MarkupGeometry.HitTest(line, new(50, 60), 2));
    }

    [Fact]
    public void TopmostHitIsTheLastDrawn()
    {
        var a = Make(MarkupKind.Highlight, new(0, 0), new(100, 100));
        var b = Make(MarkupKind.Highlight, new(50, 50), new(150, 150));
        Assert.Equal(b.Id, MarkupGeometry.HitTestTopmost([a, b], new(75, 75), 1)!.Id);
        Assert.Equal(a.Id, MarkupGeometry.HitTestTopmost([a, b], new(25, 25), 1)!.Id);
        Assert.Null(MarkupGeometry.HitTestTopmost([a, b], new(300, 300), 1));
    }

    [Fact]
    public void BoxHandlesResizeAndAllowFlipping()
    {
        var m = Make(MarkupKind.Rectangle, new(0, 0), new(100, 50));
        var handles = MarkupGeometry.Handles(m);
        Assert.Equal(8, handles.Count);
        Assert.Equal(new PdfPoint(100, 50), handles[4]);
        var resized = MarkupGeometry.DragHandle(m, 4, new(120, 80));
        Assert.Equal(new PdfRect(0, 0, 120, 80), MarkupGeometry.HandleRect(resized));
        var flipped = MarkupGeometry.DragHandle(m, 3, new(-20, 30)); // rechte Kante über die linke hinaus
        Assert.Equal(new PdfRect(-20, 0, 0, 50), MarkupGeometry.HandleRect(flipped));
        Assert.Equal(m.Id, resized.Id);
    }

    [Fact]
    public void VertexHandlesMoveSinglePoints()
    {
        var m = Make(MarkupKind.Polygon, new(0, 0), new(100, 0), new(50, 80));
        Assert.Equal(3, MarkupGeometry.Handles(m).Count);
        var moved = MarkupGeometry.DragHandle(m, 2, new(50, 120));
        Assert.Equal(new PdfPoint(50, 120), moved.Points[2]);
        Assert.Equal(m.Points[0], moved.Points[0]);
        Assert.Empty(MarkupGeometry.Handles(Make(MarkupKind.Note, new PdfPoint(10, 10))));
    }

    [Fact]
    public void FreehandScalesWithItsFrame()
    {
        var m = Markup.Create(MarkupKind.Freehand, [new PdfPoint[] { new(0, 0), new(10, 10) }, new PdfPoint[] { new(5, 0), new(10, 5) }], Red, "Test");
        var scaled = MarkupGeometry.ResizeTo(m, new PdfRect(0, 0, 20, 40));
        Assert.Equal(new PdfPoint(20, 40), scaled.Paths[0][1]);
        Assert.Equal(new PdfPoint(10, 0), scaled.Paths[1][0]);
    }

    [Fact]
    public void TranslateMovesAllPaths()
    {
        var m = Make(MarkupKind.Polyline, new(0, 0), new(10, 0), new(10, 10));
        var t = MarkupGeometry.Translate(m, 5, -5);
        Assert.Equal([new PdfPoint(5, -5), new PdfPoint(15, -5), new PdfPoint(15, 5)], t.Points);
    }

    [Fact]
    public void SimplifyDropsCollinearPoints()
    {
        var pts = Enumerable.Range(0, 101).Select(i => new PdfPoint(i, i < 50 ? 0 : i - 50)).ToArray();
        var s = MarkupGeometry.Simplify(pts, 0.1);
        Assert.Equal([new PdfPoint(0, 0), new PdfPoint(50, 0), new PdfPoint(100, 50)], s);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void TextFrameMatchesPageDisplayOrientation(int rotation)
    {
        // Auf einer gedrehten Seite muss lokales „rechts“ und „unten“ auf dem Bildschirm rechts und unten sein.
        var transform = new PageTransform(new PdfRect(0, 0, 600, 400), rotation);
        var box = new PdfRect(100, 100, 300, 200);
        var frame = TextFrame.ForBox(box, rotation);
        var (u0, v0) = transform.ToDisplay(frame.ToUser(new(0, 0)));
        var (u1, v1) = transform.ToDisplay(frame.ToUser(new(frame.Width, frame.Height)));
        Assert.True(u1 > u0 && v1 > v0);
        // Der Rahmen deckt genau das Rechteck ab.
        var corners = new[] { frame.ToUser(new(0, 0)), frame.ToUser(new(frame.Width, frame.Height)) };
        Assert.Equal(box, PdfRect.FromPoints(corners[0], corners[1]));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void PageTransformRoundTripsAndMatrixAgrees(int rotation)
    {
        var t = new PageTransform(new PdfRect(10, 20, 610, 420), rotation);
        var p = new PdfPoint(123.5, 321.25);
        var (u, v) = t.ToDisplay(p);
        Assert.Equal(p, t.ToUser(u, v));
        var m = t.Matrix;
        Assert.Equal(u, p.X * m.M11 + p.Y * m.M21 + m.OffsetX, 9);
        Assert.Equal(v, p.X * m.M12 + p.Y * m.M22 + m.OffsetY, 9);
        Assert.InRange(u, 0, t.DisplayWidth);
        Assert.InRange(v, 0, t.DisplayHeight);
    }

    // ---------- Zeichnen ----------

    [Fact]
    public void DraggingCreatesRectangleAndClickAloneDoesNot()
    {
        var d = new MarkupDrawing(MarkupKind.Rectangle, Red, "Test", 0, 2);
        d.Press(new(10, 10));
        d.Move(new(50, 40));
        Assert.NotNull(d.Preview);
        var m = d.Release(new(60, 50));
        Assert.NotNull(m);
        Assert.Equal([new PdfPoint(10, 10), new PdfPoint(60, 50)], m!.Points);
        Assert.False(d.IsActive);

        d.Press(new(10, 10));
        Assert.Null(d.Release(new(10.5, 10)));
    }

    [Fact]
    public void ClickPlacesNoteAndDefaultTextBox()
    {
        var note = new MarkupDrawing(MarkupKind.Note, Red, "Test", 0, 2);
        note.Press(new(100, 100));
        Assert.Equal(new PdfPoint(100, 100), note.Release(new(100, 100))!.Points.Single());

        var text = new MarkupDrawing(MarkupKind.TextBox, Red, "Test", 0, 2);
        text.Press(new(100, 500));
        var box = MarkupGeometry.HandleRect(text.Release(new(100, 500))!);
        Assert.Equal(new PdfRect(100, 460, 260, 500), box); // oben links am Klickpunkt
    }

    [Fact]
    public void PolygonCollectsClicksUntilFinished()
    {
        var d = new MarkupDrawing(MarkupKind.Cloud, Red, "Test", 0, 2);
        foreach (var p in new PdfPoint[] { new(0, 0), new(100, 0), new(100, 100) })
        {
            d.Press(p);
            Assert.Null(d.Release(p));
        }
        d.Move(new(0, 100));
        Assert.Equal(MarkupKind.Cloud, d.Preview!.Kind);
        // Doppelklick: zweiter Klick am selben Punkt
        d.Press(new(0, 100));
        d.Release(new(0, 100));
        d.Press(new(0, 100));
        d.Release(new(0, 100));
        var m = d.Finish();
        Assert.Equal([new PdfPoint(0, 0), new PdfPoint(100, 0), new PdfPoint(100, 100), new PdfPoint(0, 100)], m!.Points);
    }

    [Fact]
    public void PolygonNeedsThreePoints()
    {
        var d = new MarkupDrawing(MarkupKind.Polygon, Red, "Test", 0, 2);
        d.Press(new(0, 0));
        d.Release(new(0, 0));
        d.Press(new(10, 0));
        d.Release(new(10, 0));
        Assert.Null(d.Finish());
    }

    [Fact]
    public void FreehandIsSimplified()
    {
        var d = new MarkupDrawing(MarkupKind.Freehand, Red, "Test", 0, 2);
        d.Press(new(0, 0));
        for (var i = 1; i <= 100; i++)
        {
            d.Move(new(i, 0));
        }
        var m = d.Release(new(100, 0));
        Assert.Equal([new PdfPoint(0, 0), new PdfPoint(100, 0)], m!.Points);
    }

    // ---------- Daten ----------

    [Fact]
    public void JsonRoundTripKeepsGeometryAndStyle()
    {
        var m = Markup.Create(MarkupKind.Cloud, [new PdfPoint[] { new(1.25, 2.5), new(100, 0), new(50, 80.125) }],
            new MarkupStyle(new PdfColor(1, 2, 3), new PdfColor(250, 240, 230), 0.5, 1.75, 14), "Elias", 90, "Prüfen");
        var json = MarkupSerializer.ToJson(m);
        var back = MarkupSerializer.FromJson(json, m.Id, m.Text, m.Author, m.Modified);
        Assert.NotNull(back);
        Assert.Equal(m.Kind, back!.Kind);
        Assert.Equal(m.Points, back.Points);
        Assert.Equal(m.Style, back.Style);
        Assert.Equal(90, back.TextRotation);
        Assert.Equal("Prüfen", back.Text);
        Assert.Equal(m.Created.ToUnixTimeMilliseconds(), back.Created.ToUnixTimeMilliseconds());
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"v\":2,\"kind\":\"Line\",\"paths\":[[0,0,1,1]]}")]
    [InlineData("{\"v\":1,\"kind\":\"Unbekannt\",\"paths\":[[0,0,1,1]]}")]
    [InlineData("{\"v\":1,\"kind\":\"Line\",\"paths\":[]}")]
    public void InvalidJsonIsIgnored(string json)
    {
        Assert.Null(MarkupSerializer.FromJson(json, "x", "", "", null));
    }

    [Fact]
    public void AppearanceStreamUsesGraphicsStateOnlyWhenTransparent()
    {
        var m = Make(MarkupKind.Rectangle, new(0, 0), new(10, 10));
        var opaque = MarkupSerializer.AppearanceStream(MarkupGeometry.BuildShape(m, null));
        Assert.DoesNotContain("/GS gs", opaque);
        Assert.Contains("0 0 m\n10 0 l\n10 10 l\n0 10 l\nh\nS\n", opaque);
        var transparent = MarkupSerializer.AppearanceStream(MarkupGeometry.BuildShape(m with { Style = m.Style with { Opacity = 0.5 } }, null));
        Assert.Contains("/GS gs", transparent);
    }

    [Fact]
    public void TextIsClippedToTheBox()
    {
        var m = Make(MarkupKind.TextBox, new(0, 0), new(100, 40)).WithText("Ein längerer Hinweistext");
        var shape = MarkupGeometry.BuildShape(m, new BlockTextOutliner());
        var text = Assert.Single(shape.Layers, l => l.IsText);
        Assert.Equal(new PdfRect(0, 0, 100, 40), text.Clip);
        Assert.Contains(" re W n", MarkupSerializer.AppearanceStream(shape));
        // Der Rahmen hängt nicht vom Text ab.
        Assert.Equal(MarkupGeometry.Bounds(m), shape.Bounds());
    }

    [Theory]
    [InlineData(0.5, "0.5")]
    [InlineData(-0.0001, "0")]
    [InlineData(1234567.891234, "1234567.891")]
    [InlineData(1e-7, "0")]
    public void NumbersAreWrittenPlainly(double value, string expected)
    {
        Assert.Equal(expected, MarkupSerializer.N(value));
    }

    [Fact]
    public void PdfDateHasOffset()
    {
        Assert.Equal("D:20261016120000+02'00'", MarkupSerializer.PdfDate(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromHours(2))));
        Assert.Equal("D:20261016120000-05'30'", MarkupSerializer.PdfDate(new DateTimeOffset(2026, 10, 16, 12, 0, 0, TimeSpan.FromMinutes(-330))));
    }
}
