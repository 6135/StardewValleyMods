namespace AnythingPonds;

/// <summary>
/// The subset of the Generic Mod Config Menu API used by this mod.
/// </summary>
public interface IGenericModConfigMenuApi
{
    /// <summary>Register a mod whose config can be edited through the UI.</summary>
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    /// <summary>Add a boolean option at the current position in the form.</summary>
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

    /// <summary>Add an integer option at the current position in the form.</summary>
    void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip = null, int? min = null, int? max = null, int? interval = null, Func<int, string>? formatValue = null, string? fieldId = null);
}
