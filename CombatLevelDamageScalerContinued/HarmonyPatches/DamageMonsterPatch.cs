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

        float scale = 1f + (who.CombatLevel * ModEntry.Config.DamageScalePerLevel);
        minDamage = (int)(minDamage * scale);
        maxDamage = (int)(maxDamage * scale);
    }
}
