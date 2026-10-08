using StardewValley;
using Xunit;
using static ProfitCalculator.Utils;

namespace ProfitCalculator.Tests;

public class UtilsTests
{
    [Theory]
    [InlineData("Raw", true)]
    [InlineData("FruitTrees", true)]
    [InlineData("WildTrees", true)]
    [InlineData("(BC)12", false)]
    [InlineData("(BC)12>(BC)163", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSoldRaw_OnlyForRawAndTreeViews(string? produceType, bool expected)
    {
        Assert.Equal(expected, IsSoldRaw(produceType));
    }

    [Theory]
    [InlineData("Raw", false)]
    [InlineData("FruitTrees", true)]
    [InlineData("WildTrees", true)]
    [InlineData("(BC)12", false)]
    [InlineData(null, false)]
    public void IsTreeView_OnlyForTreeViews(string? produceType, bool expected)
    {
        Assert.Equal(expected, IsTreeView(produceType));
    }

    [Theory]
    [InlineData(FertilizerQuality.None, 0)]
    [InlineData(FertilizerQuality.Basic, 100)]
    [InlineData(FertilizerQuality.Quality, 150)]
    [InlineData(FertilizerQuality.Deluxe, 200)]
    [InlineData(FertilizerQuality.SpeedGro, 100)]
    [InlineData(FertilizerQuality.DeluxeSpeedGro, 150)]
    [InlineData(FertilizerQuality.HyperSpeedGro, 200)]
    [InlineData((FertilizerQuality)99, 0)]
    public void FertilizerPrices_MatchShopPrices(FertilizerQuality quality, int expected)
    {
        Assert.Equal(expected, FertilizerPrices(quality));
    }

    [Theory]
    [InlineData(UtilsSeason.Spring, Season.Spring)]
    [InlineData(UtilsSeason.Summer, Season.Summer)]
    [InlineData(UtilsSeason.Fall, Season.Fall)]
    [InlineData(UtilsSeason.Winter, Season.Winter)]
    public void SeasonFromUtilsSeason_MapsByName(UtilsSeason season, Season expected)
    {
        Assert.Equal(expected, SeasonFromUtilsSeason(season));
    }

    [Fact]
    public void SeasonFromUtilsSeason_GreenhouseIsNotSupported()
    {
        Assert.Throws<NotSupportedException>(() => SeasonFromUtilsSeason(UtilsSeason.Greenhouse));
    }

    [Fact]
    public void UtilsSeason_SharesNumericValuesWithGameSeason()
    {
        // PlantData and PlantingCalendar cast between the two enums
        foreach (var season in new[] { Season.Spring, Season.Summer, Season.Fall, Season.Winter })
        {
            Assert.Equal((int)season, (int)Enum.Parse<UtilsSeason>(season.ToString()));
        }
    }
}
