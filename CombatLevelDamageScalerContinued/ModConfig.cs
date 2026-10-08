namespace CombatLevelDamageScaler;

/// <summary>
/// The config class for this mod.
/// </summary>
public sealed class ModConfig
{
    /// <summary>
    /// Gets or sets the extra damage multiplier gained per combat level (0.05 = +5% per level).
    /// </summary>
    public float DamageScalePerLevel { get; set; } = 0.05f;
}
