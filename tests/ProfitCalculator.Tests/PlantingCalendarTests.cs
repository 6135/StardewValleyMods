using ProfitCalculator.main.models;
using StardewValley;
using Xunit;
using static ProfitCalculator.Utils;

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

    [Theory]
    [InlineData(UtilsSeason.Spring, 1, 0, Season.Spring)]
    [InlineData(UtilsSeason.Spring, 1, 27, Season.Spring)]
    [InlineData(UtilsSeason.Spring, 1, 28, Season.Summer)]
    [InlineData(UtilsSeason.Spring, 28, 1, Season.Summer)]
    [InlineData(UtilsSeason.Winter, 28, 1, Season.Spring)]
    [InlineData(UtilsSeason.Fall, 1, 28, Season.Winter)]
    [InlineData(UtilsSeason.Fall, 1, 56, Season.Spring)]
    [InlineData(UtilsSeason.Spring, 1, 112, Season.Spring)]
    public void SeasonAt_AdvancesAndWrapsYear(UtilsSeason start, int day, int after, Season expected)
    {
        Assert.Equal(expected, PlantingCalendar.SeasonAt(start, day, after));
    }

    [Fact]
    public void SeasonAt_GreenhouseCountsFromSpring()
    {
        Assert.Equal(Season.Spring, PlantingCalendar.SeasonAt(UtilsSeason.Greenhouse, 1, 0));
        Assert.Equal(Season.Summer, PlantingCalendar.SeasonAt(UtilsSeason.Greenhouse, 1, 28));
    }

    [Fact]
    public void UtilsSeasonAt_GreenhouseStaysGreenhouse()
    {
        Assert.Equal(UtilsSeason.Greenhouse, PlantingCalendar.UtilsSeasonAt(UtilsSeason.Greenhouse, 5, 200));
    }

    [Fact]
    public void UtilsSeasonAt_OtherSeasonsMatchSeasonAt()
    {
        Assert.Equal(UtilsSeason.Fall, PlantingCalendar.UtilsSeasonAt(UtilsSeason.Summer, 20, 10));
        Assert.Equal(UtilsSeason.Spring, PlantingCalendar.UtilsSeasonAt(UtilsSeason.Winter, 28, 1));
    }
}
