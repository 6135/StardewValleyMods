using ProfitCalculator.main;
using ProfitCalculator.main.models;
using Xunit;

namespace ProfitCalculator.Tests;

public class ManualCropRegistryTests
{
    [Theory]
    [InlineData("(O)472", "472")]
    [InlineData("(o)472", "472")]
    [InlineData("  (O)472 ", "472")]
    [InlineData("472", "472")]
    [InlineData("(BC)12", "(BC)12")]
    public void NormalizeId_StripsObjectPrefix(string id, string expected)
    {
        Assert.Equal(expected, ManualCropRegistry.NormalizeId(id));
    }

    [Fact]
    public void CandidateKeys_UnqualifiedGetsObjectQualifiedForm()
    {
        Assert.Equal(new[] { "472", "(O)472" }, ManualCropRegistry.CandidateKeys("472"));
    }

    [Fact]
    public void CandidateKeys_QualifiedGetsBareForm()
    {
        Assert.Equal(new[] { "(O)472", "472" }, ManualCropRegistry.CandidateKeys(" (O)472 "));
    }

    [Fact]
    public void Crops_QualifiedAndBareIdsReplaceEachOther()
    {
        var registry = new ManualCropRegistry();
        registry.SetCrop("(O)472", new ManualCropDefinition { Name = "first" });
        registry.SetCrop("472", new ManualCropDefinition { Name = "second" });

        var crops = registry.GetCrops();
        Assert.Single(crops);
        Assert.Equal("second", crops["472"].Name);
    }

    [Fact]
    public void GetCrops_ReturnsACopy()
    {
        var registry = new ManualCropRegistry();
        registry.SetCrop("472", new ManualCropDefinition());
        registry.GetCrops().Clear();
        Assert.Single(registry.GetCrops());
    }

    [Fact]
    public void RemoveCrop_AcceptsEitherForm()
    {
        var registry = new ManualCropRegistry();
        registry.SetCrop("(O)472", new ManualCropDefinition());
        Assert.True(registry.RemoveCrop("472"));
        Assert.False(registry.RemoveCrop("472"));
        Assert.Empty(registry.GetCrops());
    }

    [Theory]
    [InlineData("472")]
    [InlineData("(O)472")]
    public void SeedPrice_FoundWhateverFormItWasRegisteredWith(string lookup)
    {
        var registry = new ManualCropRegistry();
        registry.SetSeedPrice("472", 50);
        Assert.True(registry.TryGetSeedPrice(lookup, out int price));
        Assert.Equal(50, price);

        var qualified = new ManualCropRegistry();
        qualified.SetSeedPrice("(O)472", 70);
        Assert.True(qualified.TryGetSeedPrice(lookup, out price));
        Assert.Equal(70, price);
    }

    [Fact]
    public void SeedPrice_MissingReturnsFalse()
    {
        var registry = new ManualCropRegistry();
        Assert.False(registry.TryGetSeedPrice("472", out int price));
        Assert.Equal(0, price);
    }

    [Fact]
    public void SeedPrice_NegativeRemovesOverride()
    {
        var registry = new ManualCropRegistry();
        registry.SetSeedPrice("472", 50);
        registry.SetSeedPrice("(O)472", -1);
        Assert.False(registry.TryGetSeedPrice("472", out _));
    }

    [Fact]
    public void SeedPrice_ZeroIsAValidOverride()
    {
        var registry = new ManualCropRegistry();
        registry.SetSeedPrice("472", 0);
        Assert.True(registry.TryGetSeedPrice("472", out int price));
        Assert.Equal(0, price);
    }
}
