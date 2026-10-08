using CombatLevelDamageScaler;
using CombatLevelDamageScaler.HarmonyPatches;

using Xunit;

namespace CombatLevelDamageScalerContinued.Tests;

public class DamageScalingTests
{
    [Fact]
    public void ConfigDefaultsToFivePercentPerLevel()
        => Assert.Equal(0.05f, new ModConfig().DamageScalePerLevel);

    [Theory]
    [InlineData(10, 0, 0.05f, 10)]
    [InlineData(10, 10, 0.05f, 15)]
    [InlineData(20, 5, 0.1f, 30)]
    [InlineData(10, 5, 0f, 10)]
    [InlineData(0, 10, 0.05f, 0)]
    public void ScalesLinearlyWithCombatLevel(int damage, int level, float perLevel, int expected)
        => Assert.Equal(expected, DamageMonsterPatch.ScaleDamage(damage, level, perLevel));

    [Fact]
    public void TruncatesFractionalDamage()
        => Assert.Equal(10, DamageMonsterPatch.ScaleDamage(9, 3, 0.05f));
}
