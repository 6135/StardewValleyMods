using StardewValley.GameData.FishPonds;

namespace AnythingPonds;

/// <summary>
/// The pond entries this mod adds to <c>Data/FishPondData</c>.
/// </summary>
internal static class PondData
{
    /// <summary>The seaweed and algae item IDs (unqualified, as stored in <c>FishPond.fishType</c>).</summary>
    internal static readonly string[] AlgaeIds = { "152", "153", "157" };

    private const string Prefix = "6135.AnythingPonds_";

    /// <summary>Adds this mod's entries to the pond data.</summary>
    /// <param name="data">The pond data list.</param>
    /// <param name="config">The mod config.</param>
    internal static void Edit(IList<FishPondData> data, ModConfig config)
    {
        if (config.EnableAlgaePonds)
        {
            data.Add(Algae("Seaweed", "152"));
            data.Add(Algae("GreenAlgae", "153"));
            data.Add(Algae("WhiteAlgae", "157"));
        }

        if (config.EnableGenericPonds)
        {
            data.Add(new FishPondData
            {
                Id = Prefix + "Generic",
                RequiredTags = new() { "!" + HarmonyPatches.FishPondDoActionPatch.ExcludeTag },
                Precedence = 10000,
                SpawnTime = 999999,
                ProducedItems = new() { Reward("Rotten", 10, 0.01f, "(O)747", 1, 1) },
            });
        }
    }

    private static FishPondData Algae(string name, string id) => new()
    {
        Id = Prefix + name,
        RequiredTags = new() { "id_o_" + id },
        Precedence = 1, // any other mod's entry for these items wins
        SpawnTime = 2,
        PopulationGates = new()
        {
            [4] = new() { "(O)368 3" },
            [7] = new() { "(O)369 5" },
        },
        ProducedItems = new()
        {
            Reward("Self9", 9, 0.85f, "(O)" + id, 2, 3),
            Reward("Bait9", 9, 0.33f, "(O)774", 5, 5),
            Reward("Fiber9", 9, 1f, "(O)771", 3, 3),
            Reward("Self6", 6, 0.75f, "(O)" + id, 2, 2),
            Reward("Fiber6", 6, 0.8f, "(O)771", 2, 3),
            Reward("Self3", 3, 0.75f, "(O)" + id, 1, 2),
            Reward("Fiber3", 3, 0.8f, "(O)771", 2, 2),
            Reward("Self1", 1, 0.65f, "(O)" + id, 1, 1),
            Reward("Fiber1", 1, 1f, "(O)771", 1, 1),
        },
    };

    private static FishPondReward Reward(string id, int population, float chance, string itemId, int min, int max) => new()
    {
        Id = id,
        RequiredPopulation = population,
        Chance = chance,
        ItemId = itemId,
        MinStack = min,
        MaxStack = max,
    };
}
