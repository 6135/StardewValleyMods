using Xunit;

namespace GiantCropFertilizer.Tests;

public class ModConfigTests
{
    [Fact]
    public void Defaults_AlwaysGrowAndStayOnFarm()
    {
        ModConfig config = new();
        Assert.Equal(1.1d, config.GiantCropChance);
        Assert.False(config.AllowGiantCropsOffFarm);
    }

    [Theory]
    [InlineData(-5, 0)]
    [InlineData(0, 0)]
    [InlineData(0.5, 0.5)]
    [InlineData(1, 1)]
    [InlineData(1.1, 1.1)]
    [InlineData(7, 1.1)]
    public void GiantCropChance_IsClamped(double value, double expected)
        => Assert.Equal(expected, new ModConfig { GiantCropChance = value }.GiantCropChance);
}

public class FertilizerIdTests
{
    private const string Id = ModEntry.GiantCropFertilizerID;
    private const string Qualified = ModEntry.QualifiedGiantCropFertilizerID;

    [Fact]
    public void QualifiedId_IsObjectPrefixedId() => Assert.Equal("(O)" + Id, Qualified);

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("369", false)]
    [InlineData(Id, true)]
    [InlineData(Qualified, true)]
    public void IsGiantCropFertilizer_MatchesBothForms(string? value, bool expected)
        => Assert.Equal(expected, FertilizerValues.IsGiantCropFertilizer(value));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("(O)369", false)]
    [InlineData(Qualified, true)]
    [InlineData("(O)369|" + Qualified, true)]
    [InlineData(Id + "|(O)370", true)]
    [InlineData("(O)369|(O)370", false)]
    public void HasGiantCropFertilizer_SearchesPipeSeparatedList(string? value, bool expected)
        => Assert.Equal(expected, FertilizerValues.HasGiantCropFertilizer(value));

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(Qualified, null)]
    [InlineData("(O)369", "(O)369")]
    [InlineData("(O)369|" + Qualified, "(O)369")]
    [InlineData(Id + "|(O)369|(O)370", "(O)369|(O)370")]
    [InlineData("(O)369||(O)370", "(O)369|(O)370")]
    public void RemoveGiantCropFertilizer_KeepsOthers(string? value, string? expected)
        => Assert.Equal(expected, FertilizerValues.RemoveGiantCropFertilizer(value));
}
