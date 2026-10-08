using GingerIslandMainlandAdjustments.Configuration;

using Xunit;

using GimaDayOfWeek = GingerIslandMainlandAdjustments.Configuration.DayOfWeek;

namespace GingerIslandMainlandAdjustmentsContinued.Tests;

public class ModConfigTests
{
    [Fact]
    public void Defaults()
    {
        ModConfig config = new();

        Assert.False(config.EnforceGITiming);
        Assert.True(config.RequireResortDialogue);
        Assert.Equal(WearIslandClothing.Default, config.WearIslandClothing);
        Assert.True(config.UseThisScheduler);
        Assert.Equal(6, config.Capacity);
        Assert.True(config.StageFarNpcsAtSaloon);
        Assert.Equal(0.6f, config.GroupChance);
        Assert.Equal(0.05f, config.ExplorerChance);
        Assert.Equal(GimaDayOfWeek.Tuesday, config.GusDay);
        Assert.Equal(0.5f, config.GusChance);
        Assert.Equal(VillagerExclusionOverride.Yes, config.AllowWilly);
        Assert.Equal(VillagerExclusionOverride.Yes, config.AllowSandy);
        Assert.True(config.AllowGeorgeAndEvelyn);
        Assert.Equal(VillagerExclusionOverride.IfMarried, config.AllowWizard);
        Assert.Empty(config.ScheduleStrictness);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(7, 7)]
    [InlineData(15, 15)]
    [InlineData(100, 15)]
    public void CapacityIsClamped(int value, int expected)
    {
        ModConfig config = new() { Capacity = value };
        Assert.Equal(expected, config.Capacity);
    }

    [Theory]
    [InlineData(-0.5f, 0f)]
    [InlineData(0.25f, 0.25f)]
    [InlineData(1f, 1f)]
    [InlineData(3f, 1f)]
    public void ChancesAreClampedToUnitRange(float value, float expected)
    {
        ModConfig config = new() { GroupChance = value, ExplorerChance = value, GusChance = value };

        Assert.Equal(expected, config.GroupChance);
        Assert.Equal(expected, config.ExplorerChance);
        Assert.Equal(expected, config.GusChance);
    }

    [Fact]
    public void UndefinedClothingValueFallsBackToDefault()
    {
        ModConfig config = new() { WearIslandClothing = WearIslandClothing.All };
        config.WearIslandClothing = (WearIslandClothing)99;

        Assert.Equal(WearIslandClothing.Default, config.WearIslandClothing);
    }

    [Theory]
    [InlineData(GimaDayOfWeek.None, "None")]
    [InlineData(GimaDayOfWeek.Monday, "Mon")]
    [InlineData(GimaDayOfWeek.Tuesday, "Tue")]
    [InlineData(GimaDayOfWeek.Wednesday, "Wed")]
    [InlineData(GimaDayOfWeek.Thursday, "Thu")]
    [InlineData(GimaDayOfWeek.Friday, "Fri")]
    [InlineData(GimaDayOfWeek.Saturday, "Sat")]
    [InlineData(GimaDayOfWeek.Sunday, "Sun")]
    [InlineData((GimaDayOfWeek)42, "Tue")]
    public void GusDayAsShortString(GimaDayOfWeek day, string expected)
    {
        ModConfig config = new() { GusDay = day };
        Assert.Equal(expected, config.GusDayAsShortString());
    }
}
