using CapstoneProfessions.Framework;

using Xunit;

namespace CapstoneProfessionsContinued.Tests;

public class ProfessionScalingTests
{
    [Theory]
    [InlineData(0, 1f)]
    [InlineData(1, 1.05f)]
    [InlineData(2, 1.1f)]
    [InlineData(4, 1.2f)]
    public void NameBrandAddsFivePercentPerHolder(int holders, float expected)
        => Assert.Equal(expected, ProfessionScaling.NameBrandMultiplier(holders), 4);

    [Theory]
    [InlineData(7000, 0, 7000)]
    [InlineData(7000, 1, 8400)]
    [InlineData(7000, 2, 9800)]
    [InlineData(7000, 4, 12600)]
    public void TimelapseAddsTwentyPercentPerHolder(int baseInterval, int holders, int expected)
        => Assert.InRange(ProfessionScaling.TimelapseInterval(baseInterval, holders), expected - 1, expected);

    [Fact]
    public void TimelapseTruncatesToInt()
        => Assert.Equal(1, ProfessionScaling.TimelapseInterval(1, 1));
}
