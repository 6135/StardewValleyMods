using Xunit;

namespace HolidaySales.Tests;

public class FestivalRegionsTests
{
    [Theory]
    [InlineData("Town", "Town")]
    [InlineData("Beach", "Town")]
    [InlineData("Forest_Cave", "Forest")]
    [InlineData("Custom_Mod_Map", "Mod")]
    [InlineData("Custom_Map", FestivalRegions.CustomMapRegion)]
    [InlineData("_Map", "Town")]
    [InlineData("Custom__Map", "Town")]
    public void RegionFromMapName_DerivesRegion(string map, string expected)
        => Assert.Equal(expected, FestivalRegions.RegionFromMapName(map));

    [Theory]
    [InlineData("Town", "Town", true)]
    [InlineData("Forest", "Forest", true)]
    [InlineData("Forest", "Town", true)]
    [InlineData("Forest", "Beach", false)]
    [InlineData("Custom_Mod", FestivalRegions.CustomMapRegion, true)]
    [InlineData("Custom_Mod_Map", FestivalRegions.CustomMapRegion, false)]
    [InlineData("Custom_Mod", "Town", false)]
    [InlineData("Custom_Mod", "Forest", false)]
    [InlineData("Custom_Mod_Map", "Custom_Mod_Map", true)]
    public void ConditionsMatchRegion_AppliesFestivalLocationRules(string conditions, string region, bool expected)
        => Assert.Equal(expected, FestivalRegions.ConditionsMatchRegion(conditions, region));

    [Fact]
    public void Config_DefaultsToMapDependent()
        => Assert.Equal(FestivalsShopBehavior.MapDependent, new ModConfig().StoreFestivalBehavior);
}
