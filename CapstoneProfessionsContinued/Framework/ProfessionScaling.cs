namespace CapstoneProfessions.Framework;

/// <summary>Multiplier math for the capstone professions.</summary>
internal static class ProfessionScaling
{
    /// <summary>Gets the shipping price multiplier for the given number of players with the Name Brand profession.</summary>
    /// <param name="holders">The number of players with the profession.</param>
    /// <returns>The multiplier.</returns>
    internal static float NameBrandMultiplier(int holders)
    {
        float mult = 1;
        for (int i = 0; i < holders; i++)
        {
            mult += 0.05f;
        }

        return mult;
    }

    /// <summary>Gets the ten-minute interval, in real milliseconds, for the given number of players with the Timelapse profession.</summary>
    /// <param name="baseInterval">The vanilla interval.</param>
    /// <param name="holders">The number of players with the profession.</param>
    /// <returns>The lengthened interval.</returns>
    internal static int TimelapseInterval(int baseInterval, int holders)
    {
        float mult = 1;
        for (int i = 0; i < holders; i++)
        {
            mult += 0.2f;
        }

        return (int)(baseInterval * mult);
    }
}
