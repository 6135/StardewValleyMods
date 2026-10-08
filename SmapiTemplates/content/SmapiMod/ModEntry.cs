using StardewModdingAPI;
using StardewModdingAPI.Events;

namespace SmapiMod;

internal sealed class ModEntry : Mod
{
    private ModConfig config = null!;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<ModConfig>();
        helper.Events.Input.ButtonPressed += OnButtonPressed;
    }

    private void OnButtonPressed(object? sender, ButtonPressedEventArgs e)
    {
        if (!Context.IsWorldReady || !config.ExampleOption)
            return;

        Monitor.Log(Helper.Translation.Get("button-pressed", new { button = e.Button }), LogLevel.Debug);
    }
}
