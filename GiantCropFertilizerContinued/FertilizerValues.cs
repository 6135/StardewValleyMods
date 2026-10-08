namespace GiantCropFertilizer;

/// <summary>
/// Reads and edits <see cref="StardewValley.TerrainFeatures.HoeDirt.fertilizer"/> values. Kept apart from
/// <see cref="ModEntry"/> so it has no SMAPI dependency.
/// </summary>
internal static class FertilizerValues
{
    private const char FertilizerSeparator = '|';

    /// <summary>
    /// Checks whether a fertilizer ID (qualified or not) is the giant crop fertilizer.
    /// </summary>
    /// <param name="fertilizer">Fertilizer to check.</param>
    /// <returns>True if matches, false otherwise.</returns>
    internal static bool IsGiantCropFertilizer(string? fertilizer)
        => fertilizer is ModEntry.GiantCropFertilizerID or ModEntry.QualifiedGiantCropFertilizerID;

    /// <summary>
    /// Checks whether a <see cref="StardewValley.TerrainFeatures.HoeDirt.fertilizer"/> value holds the giant crop fertilizer.
    /// Ultimate Fertilizer stores several fertilizers as <c>a|b|c</c>.
    /// </summary>
    /// <param name="value">The soil's fertilizer value.</param>
    /// <returns>True if the giant crop fertilizer is one of them.</returns>
    internal static bool HasGiantCropFertilizer(string? value)
        => value is not null && value.Split(FertilizerSeparator).Any(IsGiantCropFertilizer);

    /// <summary>
    /// Removes the giant crop fertilizer from a soil's fertilizer value, keeping any others.
    /// </summary>
    /// <param name="value">The soil's fertilizer value.</param>
    /// <returns>The remaining fertilizers, or null if none are left.</returns>
    internal static string? RemoveGiantCropFertilizer(string? value)
    {
        string remaining = string.Join(FertilizerSeparator, (value ?? string.Empty).Split(FertilizerSeparator).Where(id => id.Length > 0 && !IsGiantCropFertilizer(id)));
        return remaining.Length > 0 ? remaining : null;
    }
}
