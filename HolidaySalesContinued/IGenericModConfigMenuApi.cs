namespace HolidaySales;

/// <summary>
/// The subset of the Generic Mod Config Menu API used by this mod.
/// </summary>
public interface IGenericModConfigMenuApi
{
    /// <summary>Register a mod whose config can be edited through the UI.</summary>
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    /// <summary>Add a string option at the current position in the form.</summary>
    void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip = null, string[]? allowedValues = null, Func<string, string>? formatAllowedValue = null, string? fieldId = null);
}
