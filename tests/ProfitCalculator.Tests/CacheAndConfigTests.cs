using ProfitCalculator.main.memory;
using ProfitCalculator.main.models;
using StardewModdingAPI;
using Xunit;

namespace ProfitCalculator.Tests;

public class CacheAndConfigTests
{
    [Fact]
    public void ModConfig_Defaults()
    {
        var config = new ModConfig();
        Assert.Equal(SButton.F8, config.HotKey);
        Assert.Equal(30, config.ToolTipDelay);
    }

    [Fact]
    public void ManualCropDefinition_Defaults()
    {
        var def = new ManualCropDefinition();
        Assert.Equal(-1, def.RegrowthTime);
        Assert.Equal(1, def.MinimumHarvests);
        Assert.Equal(1, def.MaximumHarvests);
        Assert.True(def.HasQuality);
        Assert.True(def.AcceptsFertilizer);
        Assert.False(def.IsPaddyCrop);
    }

    [Fact]
    public void PlantGrowth_SingleDropYieldsExactlyOne()
    {
        var growth = PlantGrowth.SingleDrop(28, 1);
        Assert.Equal(28, growth.Days);
        Assert.Equal(1, growth.RegrowDays);
        Assert.Equal(1, growth.MinHarvests);
        Assert.Equal(1, growth.MaxHarvests);
        Assert.Equal(0f, growth.MaxHarvestIncreasePerFarmingLevel);
        Assert.Equal(0d, growth.ChanceForExtraCrops);
    }

    [Fact]
    public void Cache_BuildsOnCreationAndRebuildsAfterInvalidation()
    {
        int builds = 0;
        var cache = new Cache<int>(() => ++builds);

        Assert.Equal(1, cache.GetCache());
        Assert.Equal(1, cache.GetCache());
        Assert.True(cache.IsCacheValid());

        cache.InvalidateCache();
        Assert.False(cache.IsCacheValid());
        Assert.Equal(2, cache.GetCache());
        Assert.True(cache.IsCacheValid());
    }

    [Fact]
    public void Cache_SetBuildCacheTakesEffectOnRebuild()
    {
        var cache = new Cache<string>(() => "a");
        cache.SetBuildCache(() => "b");
        Assert.Equal("a", cache.GetCache());
        cache.RebuildCache();
        Assert.Equal("b", cache.GetCache());
    }

    [Fact]
    public void Cache_ClearInvalidates()
    {
        var cache = new Cache<string>(() => "a");
        cache.ClearCache();
        Assert.False(cache.IsCacheValid());
        Assert.Equal("a", cache.GetCache());
    }

    [Fact]
    public void Cache_NullBuilderThrows()
    {
        Assert.Throws<ArgumentNullException>(() => new Cache<int>(null!));
    }
}
