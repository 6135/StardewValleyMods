namespace HolidaySales;

/// <summary>
/// Pure festival region matching, kept free of game state.
/// </summary>
internal static class FestivalRegions
{
    /// <summary>
    /// The region assigned to maps named <c>Custom_X</c> with no further underscore.
    /// </summary>
    internal const string CustomMapRegion = "CustomMapRegion";

    /// <summary>
    /// Derives a region from a map name, for mods that don't use their own location context: <c>Region_Map</c> or <c>Custom_Mod_Map</c>.
    /// </summary>
    /// <param name="mapname">The map name.</param>
    /// <returns>The region name.</returns>
    internal static string RegionFromMapName(string mapname)
    {
        string name = mapname;
        string defaultArea = "Town";
        if (name.StartsWith("Custom_", StringComparison.Ordinal))
        {
            name = name["Custom_".Length..];
            defaultArea = CustomMapRegion;
        }
        int index = name.IndexOf('_');
        string region = index == -1 ? defaultArea : name[..index];
        return region.Length == 0 ? "Town" : region;
    }

    /// <summary>
    /// Whether a festival's location condition applies to a map region.
    /// </summary>
    /// <param name="conditions">The festival's location condition (first segment of its <c>conditions</c> entry).</param>
    /// <param name="mapRegion">The map's region.</param>
    /// <returns>true if the festival closes shops in that region.</returns>
    internal static bool ConditionsMatchRegion(string conditions, string mapRegion)
    {
        if (conditions == mapRegion)
        {
            return true;
        }
        bool isCustom = conditions.StartsWith("Custom", StringComparison.Ordinal);
        if (!isCustom && mapRegion == "Town")
        {
            return true;
        }
        if (isCustom && mapRegion == CustomMapRegion)
        {
            return conditions.IndexOf('_') == conditions.LastIndexOf('_');
        }
        return false;
    }
}
