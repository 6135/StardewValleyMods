namespace AnythingPonds;

/// <summary>
/// The config class for this mod.
/// </summary>
public sealed class ModConfig
{
    /// <summary>Gets or sets a value indicating whether seaweed and algae get pond data.</summary>
    public bool EnableAlgaePonds { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether any item without pond data gets a generic catch-all entry.</summary>
    public bool EnableGenericPonds { get; set; } = false;

    /// <summary>Gets or sets a value indicating whether empty ponds turn into algae ponds.</summary>
    public bool EmptyPondsBecomeAlgae { get; set; } = true;

    /// <summary>Gets or sets how many days a pond must stay empty before it turns into an algae pond.</summary>
    public int DaysUntilAlgae { get; set; } = 7;
}
