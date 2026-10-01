using AwesomeAssertions;
using SportFrog.Api.Features.Lists;

namespace SportFrog.Api.Tests.Features.Lists;

/// <summary>
/// The one place <see cref="ListXlsx"/> and <see cref="ListPdf"/> both read a
/// cell's meaning from. A provider's bug — a goal count written as a string,
/// a date written as a number — has to surface here, as a thrown exception,
/// rather than as two renderers quietly disagreeing about what a cell says.
/// </summary>
public sealed class ListCellValuesTests
{
    [Theory]
    [InlineData(4)]
    [InlineData(4L)]
    [InlineData((short)4)]
    [InlineData(4.5)]
    [InlineData(4.5f)]
    public void ToNumber_ANumericType_ConvertsToDouble(object value)
    {
        ListCellValues.ToNumber(value).Should().Be(Convert.ToDouble(value));
    }

    [Fact]
    public void ToNumber_ADecimal_ConvertsToDouble()
    {
        ListCellValues.ToNumber(4.5m).Should().Be(4.5d);
    }

    [Fact]
    public void ToNumber_NotANumber_Throws()
    {
        var converting = () => ListCellValues.ToNumber("4");

        converting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToBoolean_ABool_ReturnsIt()
    {
        ListCellValues.ToBoolean(true).Should().BeTrue();
    }

    [Fact]
    public void ToBoolean_NotABool_Throws()
    {
        var converting = () => ListCellValues.ToBoolean("true");

        converting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToDateTime_ADateOnly_ConvertsAtMidnight()
    {
        ListCellValues.ToDateTime(new DateOnly(2026, 5, 1)).Should().Be(new DateTime(2026, 5, 1));
    }

    [Fact]
    public void ToDateTime_ADateTimeOffset_ConvertsToLocalTime()
    {
        var offset = new DateTimeOffset(2026, 5, 1, 10, 0, 0, TimeSpan.Zero);

        ListCellValues.ToDateTime(offset).Should().Be(offset.ToLocalTime().DateTime);
    }

    [Fact]
    public void ToDateTime_NotADate_Throws()
    {
        var converting = () => ListCellValues.ToDateTime("2026-05-01");

        converting.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ToDisplayText_Null_ReturnsEmpty()
    {
        ListCellValues.ToDisplayText(null, ListValueKind.Text).Should().BeEmpty();
    }

    [Fact]
    public void ToDisplayText_Number_FormatsWithoutTrailingZeros()
    {
        ListCellValues.ToDisplayText(4, ListValueKind.Number).Should().Be("4");
        ListCellValues.ToDisplayText(4.5, ListValueKind.Number).Should().Be("4.5");
    }

    [Fact]
    public void ToDisplayText_Boolean_ReadsInSpanish()
    {
        ListCellValues.ToDisplayText(true, ListValueKind.Boolean).Should().Be("Sí");
        ListCellValues.ToDisplayText(false, ListValueKind.Boolean).Should().Be("No");
    }

    [Fact]
    public void ToDisplayText_Date_FormatsWithoutTime()
    {
        ListCellValues.ToDisplayText(new DateOnly(2026, 5, 1), ListValueKind.Date).Should().Be("01/05/2026");
    }

    [Fact]
    public void ToDisplayText_DateTime_FormatsWithTime()
    {
        var at = new DateTime(2026, 5, 1, 14, 30, 0);

        ListCellValues.ToDisplayText(at, ListValueKind.DateTime).Should().Be("01/05/2026 14:30");
    }

    [Fact]
    public void ToDisplayText_Text_ReturnsTheValueItself()
    {
        ListCellValues.ToDisplayText("Diaz", ListValueKind.Text).Should().Be("Diaz");
    }
}
