using ProfitCalculator.main.models;
using Xunit;

namespace ProfitCalculator.Tests;

public class PlantingCalendarTests
{
    [Theory]
    [InlineData(1, 0, 1)]
    [InlineData(1, 27, 28)]
    [InlineData(1, 28, 1)]
    [InlineData(28, 1, 1)]
    [InlineData(15, 14, 1)]
    [InlineData(10, 112, 10)]
    public void DayOfMonth_WrapsEvery28Days(int plantingDay, int after, int expected)
    {
        Assert.Equal(expected, PlantingCalendar.DayOfMonth(plantingDay, after));
    }
}
