using ProfitCalculator.main.models;
using StardewValley;
using Xunit;
using static ProfitCalculator.Utils;

namespace ProfitCalculator.Tests;

/// <summary>
/// Growth and harvest math of <see cref="PlantData"/>. No calculator is registered, so the optional settings
/// (cross season, farming level) take their defaults. The seed and drops are never touched by these paths.
/// </summary>
public class PlantDataMathTests
{
    private sealed class TestPlant : PlantData
    {
        private readonly float speed;

        public TestPlant(PlantGrowth growth, List<Season> seasons, float speed = 0f)
            : base(growth, "test", seasons, null!, false, false, null!)
        {
            this.speed = speed;
        }

        public override float GetAverageGrowthSpeedValueForCrop(FertilizerQuality fertilizerQuality) => speed;
    }

    private static readonly List<Season> Spring = new() { Season.Spring };
    private static readonly List<Season> SpringSummer = new() { Season.Spring, Season.Summer };

    private static PlantGrowth Growth(int days, int regrow = 0, int min = 1, int max = 1, float perLevel = 0f, double extra = 0d)
        => new(days, regrow, min, max, perLevel, extra);

    [Theory]
    [InlineData(10, 0f, 10)]
    [InlineData(10, 0.1f, 9)]
    [InlineData(10, 0.25f, 7)]
    [InlineData(10, 0.5f, 5)]
    [InlineData(10, 0.99f, 1)]
    [InlineData(10, 2f, 1)]
    [InlineData(1, 0f, 1)]
    public void GrowingDays_RemovesCeilOfSpeedBonusButNeverBelowOne(int days, float speed, int expected)
    {
        var plant = new TestPlant(Growth(days), Spring, speed);
        Assert.Equal(expected, plant.GrowingDays(FertilizerQuality.None));
    }

    [Fact]
    public void IsAvailableForCurrentSeason_ChecksSeasonList()
    {
        var plant = new TestPlant(Growth(4), SpringSummer);
        Assert.True(plant.IsAvailableForCurrentSeason(UtilsSeason.Spring));
        Assert.True(plant.IsAvailableForCurrentSeason(UtilsSeason.Summer));
        Assert.False(plant.IsAvailableForCurrentSeason(UtilsSeason.Fall));
        Assert.False(plant.IsAvailableForCurrentSeason(UtilsSeason.Winter));
    }

    [Fact]
    public void TotalAvailableDays_CountsDaysLeftInSeason()
    {
        var plant = new TestPlant(Growth(4), Spring);
        Assert.Equal(27, plant.TotalAvailableDays(UtilsSeason.Spring, 1));
        Assert.Equal(0, plant.TotalAvailableDays(UtilsSeason.Summer, 1));
    }

    [Fact]
    public void TotalAvailableDays_GreenhouseIsWholeYear()
    {
        var plant = new TestPlant(Growth(4), Spring);
        Assert.Equal(112, plant.TotalAvailableDays(UtilsSeason.Greenhouse, 15));
    }

    [Fact]
    public void TotalHarvests_NonRegrowingCropRepeatsReplanting()
    {
        var plant = new TestPlant(Growth(10), Spring);
        // 27 days left / 10 days per crop
        Assert.Equal(2, plant.TotalHarvestsWithRemainingDays(UtilsSeason.Spring, FertilizerQuality.None, 1));
    }

    [Fact]
    public void TotalHarvests_RegrowingCropHarvestsFirstThenEveryRegrowPeriod()
    {
        var plant = new TestPlant(Growth(10, regrow: 4), Spring);
        // 27 available: first at day 10, then (27 - 10) / 4 = 4.25, so 1 + 4.25 truncated to 5
        Assert.Equal(5, plant.TotalHarvestsWithRemainingDays(UtilsSeason.Spring, FertilizerQuality.None, 1));
    }

    [Fact]
    public void TotalHarvests_TooLateToGrowIsZero()
    {
        var plant = new TestPlant(Growth(10), Spring);
        Assert.Equal(0, plant.TotalHarvestsWithRemainingDays(UtilsSeason.Spring, FertilizerQuality.None, 25));
    }

    [Fact]
    public void TotalHarvests_OutOfSeasonIsZero()
    {
        var plant = new TestPlant(Growth(4), Spring);
        Assert.Equal(0, plant.TotalHarvestsWithRemainingDays(UtilsSeason.Fall, FertilizerQuality.None, 1));
    }

    [Fact]
    public void TotalHarvests_GreenhouseUsesWholeYear()
    {
        var plant = new TestPlant(Growth(8), Spring);
        Assert.Equal(14, plant.TotalHarvestsWithRemainingDays(UtilsSeason.Greenhouse, FertilizerQuality.None, 1));
    }

    [Fact]
    public void AverageCropsPerHarvest_SingleDropIsOne()
    {
        Assert.Equal(1d, new TestPlant(Growth(4), Spring).AverageCropsPerHarvest());
    }

    [Fact]
    public void AverageCropsPerHarvest_IsMidpointOfMinAndMax()
    {
        Assert.Equal(2d, new TestPlant(Growth(4, min: 1, max: 3), Spring).AverageCropsPerHarvest());
        Assert.Equal(2.5d, new TestPlant(Growth(4, min: 2, max: 3), Spring).AverageCropsPerHarvest());
    }

    [Theory]
    [InlineData(0d, 0d)]
    [InlineData(-0.5d, 0d)]
    [InlineData(0.5d, 1d)]
    [InlineData(0.9d, 9d)]
    [InlineData(1d, 9d)]
    public void AverageExtraCrops_IsGeometricExpectationCappedAtNinetyPercent(double chance, double expected)
    {
        var plant = new TestPlant(Growth(4, extra: chance), Spring);
        Assert.Equal(expected, plant.AverageExtraCropsFromRandomness(), 9);
    }

    [Fact]
    public void PaybackDay_DefaultsToNever()
    {
        Assert.Equal(-1, new TestPlant(Growth(4), Spring).PaybackDay());
    }
}
