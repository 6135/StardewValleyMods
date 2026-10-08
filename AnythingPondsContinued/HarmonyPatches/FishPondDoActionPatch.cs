using System.Reflection.Emit;

using HarmonyLib;

using StardewValley.Buildings;
using StardewValley.Objects;

using SObject = StardewValley.Object;

namespace AnythingPonds.HarmonyPatches;

/// <summary>
/// Extends the fish-only check in <see cref="FishPond.doAction"/> so any item with pond data can be thrown in.
/// Vanilla then handles the wrong-type, full-pond and add-to-pond branches itself.
/// </summary>
[HarmonyPatch(typeof(FishPond), nameof(FishPond.doAction))]
internal static class FishPondDoActionPatch
{
    /// <summary>Context tag that keeps an item out of ponds.</summary>
    internal const string ExcludeTag = "anything_ponds_exclude";

    /// <summary>Whether the held item may be thrown into a pond.</summary>
    /// <param name="obj">The held item.</param>
    /// <returns>True for what vanilla accepts, or any non-excluded item with pond data.</returns>
    public static bool CanThrowIn(SObject obj)
    {
        if (obj.Category == SObject.FishCategory || obj.QualifiedItemId is "(O)393" or "(O)397")
        {
            return true;
        }

        return obj is not Furniture
            && !obj.bigCraftable.Value
            && !obj.HasContextTag(ExcludeTag)
            && FishPond.GetRawData(obj.ItemId) is not null;
    }

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        // Inject an extra CanThrowIn check after the vanilla category check, which is left intact because other mods (Item Extensions) anchor their own patches on it.
        CodeMatcher matcher = new CodeMatcher(instructions)
            .MatchStartForward(
                new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Farmer), nameof(Farmer.ActiveObject))),
                new CodeMatch(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(Item), nameof(Item.Category))),
                new CodeMatch(i => i.opcode == OpCodes.Ldc_I4_S && Convert.ToInt32(i.operand) == SObject.FishCategory),
                new CodeMatch(i => i.opcode == OpCodes.Beq_S || i.opcode == OpCodes.Beq))
            .ThrowIfInvalid("Could not find the fish category check in FishPond.doAction.");

        // the two instructions before get_ActiveObject load `who` (ldloc closure; ldfld who).
        CodeInstruction[] loadWho = { new(matcher.InstructionAt(-2).opcode, matcher.InstructionAt(-2).operand), new(matcher.InstructionAt(-1).opcode, matcher.InstructionAt(-1).operand) };
        CodeInstruction getActive = new(matcher.Opcode, matcher.Operand);
        object accept = matcher.InstructionAt(3).operand;

        return matcher
            .Advance(4)
            .Insert(
                loadWho[0],
                loadWho[1],
                getActive,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(FishPondDoActionPatch), nameof(CanThrowIn))),
                new CodeInstruction(OpCodes.Brtrue, accept))
            .InstructionEnumeration();
    }
}
