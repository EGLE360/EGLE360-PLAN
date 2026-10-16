namespace Egle360.Pdf.Tests;

public class FormatAndDateTests
{
    [Theory]
    [InlineData(841, 1189, "A0")]
    [InlineData(1189, 841, "A0")]
    [InlineData(594, 841, "A1")]
    [InlineData(420, 594, "A2")]
    [InlineData(297, 420, "A3")]
    [InlineData(210, 297, "A4")]
    [InlineData(211.9, 298.9, "A4")] // innerhalb der Toleranz von 2 mm
    public void IsoSizes(double wMm, double hMm, string expected)
    {
        var format = PaperFormat.FromPoints(PdfUnits.ToPoints(wMm), PdfUnits.ToPoints(hMm));
        Assert.True(format.IsStandard);
        Assert.Equal(expected, format.Name);
    }

    [Theory]
    [InlineData(213, 297)]
    [InlineData(216, 279)] // US Letter
    [InlineData(1000, 707)]
    public void OtherSizesAreCustom(double wMm, double hMm)
    {
        Assert.False(PaperFormat.FromPoints(PdfUnits.ToPoints(wMm), PdfUnits.ToPoints(hMm)).IsStandard);
    }

    [Fact]
    public void DescriptionNamesOrientation()
    {
        Assert.Equal("A1 quer, 841 × 594 mm", PaperFormat.FromPoints(PdfUnits.ToPoints(841), PdfUnits.ToPoints(594)).Description);
        Assert.Equal("A4 hoch, 210 × 297 mm", PaperFormat.FromPoints(595.28, 841.89).Description);
    }

    [Fact]
    public void UnitConversionRoundTrips()
    {
        Assert.Equal(25.4, PdfUnits.ToMillimeters(72), 9);
        Assert.Equal(72, PdfUnits.ToPoints(25.4), 9);
    }

    [Theory]
    [InlineData("D:20261016120000+02'00'", 2026, 10, 16, 12, 0, 0, 120)]
    [InlineData("D:20261016120000Z", 2026, 10, 16, 12, 0, 0, 0)]
    [InlineData("D:20261016", 2026, 10, 16, 0, 0, 0, 0)]
    [InlineData("20200101083015-05'30", 2020, 1, 1, 8, 30, 15, -330)]
    public void PdfDatesAreParsed(string value, int y, int mo, int d, int h, int mi, int s, int offsetMinutes)
    {
        Assert.Equal(new DateTimeOffset(y, mo, d, h, mi, s, TimeSpan.FromMinutes(offsetMinutes)), PdfDocument.ParsePdfDate(value));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gestern")]
    [InlineData("D:20261341")]
    public void InvalidDatesGiveNull(string? value)
    {
        Assert.Null(PdfDocument.ParsePdfDate(value));
    }
}

public class PageRangeTests
{
    [Theory]
    [InlineData("1", "1", 1)]
    [InlineData(" 1 - 3 , 5 ", "1-3,5", 4)]
    [InlineData("2-2,4", "2,4", 2)]
    [InlineData("1-10", "1-10", 10)]
    public void ValidRanges(string input, string expected, int count)
    {
        var normalized = PageRange.Normalize(input, 10);
        Assert.Equal(expected, normalized);
        Assert.Equal(count, PageRange.Count(normalized));
    }

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("0")]
    [InlineData("11")]
    [InlineData("5-3")]
    [InlineData("1,,2")]
    [InlineData("1-")]
    public void InvalidRanges(string input)
    {
        Assert.Throws<FormatException>(() => PageRange.Normalize(input, 10));
    }
}
