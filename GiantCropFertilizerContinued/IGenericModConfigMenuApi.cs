namespace GiantCropFertilizer;

/// <summary>The subset of the Generic Mod Config Menu API used by this mod.</summary>
public interface IGenericModConfigMenuApi
{
    /// <summary>Register a mod whose config can be edited through the UI.</summary>
    void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);

    /// <summary>Add a paragraph of text at the current position in the form.</summary>
    void AddParagraph(IManifest mod, Func<string> text);

    /// <summary>Add a boolean option at the current position in the form.</summary>
    void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip = null, string? fieldId = null);

    /// <summary>Add a float option at the current position in the form.</summary>
    void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip = null, float? min = null, float? max = null, float? interval = null, Func<float, string>? formatValue = null, string? fieldId = null);
}
