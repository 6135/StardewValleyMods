using HarmonyLib;
using Microsoft.Xna.Framework;

namespace CombatLevelDamageScaler.HarmonyPatches;

/// <summary>
/// Scales the damage the player deals to monsters by combat level.
/// </summary>
[HarmonyPatch(
    typeof(GameLocation),
    nameof(GameLocation.damageMonster),
    new[] { typeof(Rectangle), typeof(int), typeof(int), typeof(bool), typeof(float), typeof(int), typeof(float), typeof(float), typeof(bool), typeof(Farmer), typeof(bool) })]
internal static class DamageMonsterPatch
{
    private static void Prefix(ref int minDamage, ref int maxDamage, Farmer who)
    {
        if (who is null)
        {
            return;
        }

        minDamage = ScaleDamage(minDamage, who.CombatLevel, ModEntry.Config.DamageScalePerLevel);
        maxDamage = ScaleDamage(maxDamage, who.CombatLevel, ModEntry.Config.DamageScalePerLevel);
    }

    /// <summary>
    /// Scales a damage value by combat level, truncating toward zero.
    /// </summary>
    /// <param name="damage">The base damage.</param>
    /// <param name="combatLevel">The player's combat level.</param>
    /// <param name="scalePerLevel">The extra multiplier gained per level.</param>
    /// <returns>The scaled damage.</returns>
    internal static int ScaleDamage(int damage, int combatLevel, float scalePerLevel)
        => (int)(damage * (1f + (combatLevel * scalePerLevel)));
}
