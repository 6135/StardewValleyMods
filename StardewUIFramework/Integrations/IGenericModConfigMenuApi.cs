using System;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace UIFramework
{
    /// <summary>The subset of the Generic Mod Config Menu API used by the framework (see https://github.com/spacechase0/StardewValleyMods/blob/develop/GenericModConfigMenu/IGenericModConfigMenuApi.cs).</summary>
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly);

        void AddBoolOption(IManifest mod, Func<bool> getValue, Action<bool> setValue, Func<string> name, Func<string>? tooltip, string? fieldId);

        void AddNumberOption(IManifest mod, Func<int> getValue, Action<int> setValue, Func<string> name, Func<string>? tooltip, int? min, int? max, int? interval, Func<int, string>? formatValue, string? fieldId);

        void AddNumberOption(IManifest mod, Func<float> getValue, Action<float> setValue, Func<string> name, Func<string>? tooltip, float? min, float? max, float? interval, Func<float, string>? formatValue, string? fieldId);

        void AddTextOption(IManifest mod, Func<string> getValue, Action<string> setValue, Func<string> name, Func<string>? tooltip, string[]? allowedValues, Func<string, string>? formatAllowedValue, string? fieldId);

        // TOOLS
        void AddKeybindList(IManifest mod, Func<KeybindList> getValue, Action<KeybindList> setValue, Func<string> name, Func<string>? tooltip, string? fieldId);
    }
}
