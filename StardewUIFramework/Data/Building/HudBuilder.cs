using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// Builds a data HUD (a <c>Huds</c> entry) through the owner's facade (<see cref="StardewUIApi.CreateHud"/>). A
    /// changed entry rebuilds <b>in place</b> on the widget's inner menu, so the widget object and the player's
    /// dragged position survive. The widget is shown while its per-screen data visibility (<c>ShowHud</c> /
    /// <c>HideHud</c> / <c>ToggleHud</c> / <c>Hotkey</c>) and its <c>ShowWhen</c> expression both hold; becoming shown
    /// counts as an "open" (one-time values re-evaluate).
    /// </summary>
    internal sealed class HudBuilder
    {
        private readonly DataBuilder builder;
        private readonly IValueResolver resolver;

        internal HudBuilder(DataBuilder builder, IValueResolver resolver)
        {
            this.builder = builder;
            this.resolver = resolver;
        }

        /// <summary>Create (first build) or rebuild in place the widget of <paramref name="runtime"/>.</summary>
        internal void Build(StardewUIApi api, DataHudRuntime runtime)
        {
            UIHud hud = runtime.Hud ??= (UIHud)api.CreateHud(runtime.Id);
            hud.Inner.RebuildInPlace(_ => BuildInto(api, runtime, hud));
        }

        private void BuildInto(StardewUIApi api, DataHudRuntime runtime, UIHud hud)
        {
            runtime.ResetBuild();
            HudDefinition def = runtime.Definition;
            DataPath path = runtime.Path;
            var applier = new PropertyApplier(resolver, runtime.Refreshers, runtime.Messages);
            DataScope scope = DataScope.ForRuntime(runtime, hud.Inner);
            runtime.Scope = scope;
            builder.RegisterState(runtime, def.State, def.Computed, def.Watch, scope, path, runtime.Messages);
            DataBuilder.RegisterSources(runtime, def.Sources);
            runtime.DefaultVisible = applier.Initial(def.Visible, ValueParsers.Bool, true, scope, path.Field("Visible"));

            // options
            IUIHud h = hud;
            applier.ApplyOr(def.Anchor, ValueParsers.Anchor, UIAnchor.TopLeft, scope, path.Field("Anchor"), v => h.Anchor = v);
            applier.ApplyOr(def.X, ValueParsers.Int, 0, scope, path.Field("X"), v => h.X = v);
            applier.ApplyOr(def.Y, ValueParsers.Int, 0, scope, path.Field("Y"), v => h.Y = v);
            applier.ApplyOr(def.Width, ValueParsers.OptionalInt, null, scope, path.Field("Width"), v => h.Width = v);
            applier.ApplyOr(def.Height, ValueParsers.OptionalInt, null, scope, path.Field("Height"), v => h.Height = v);
            applier.ApplyOr(def.DrawBox, ValueParsers.Bool, true, scope, path.Field("DrawBox"), v => h.DrawBox = v);
            applier.ApplyOr(def.Opacity, ValueParsers.Float, 1f, scope, path.Field("Opacity"), v => h.Opacity = v);
            applier.ApplyOr(def.Interactive, ValueParsers.Bool, false, scope, path.Field("Interactive"), v => h.Interactive = v);
            applier.ApplyOr(def.ShowOverMenus, ValueParsers.Bool, false, scope, path.Field("ShowOverMenus"), v => h.ShowOverMenus = v);

            // the player's dragged position wins over the definition's offset
            hud.ConsumerLayout = null;
            UIServices.Layouts?.Apply(hud);

            // root stack
            applier.ApplyOr(def.Horizontal, ValueParsers.Bool, false, scope, path.Field("Horizontal"), v => hud.Inner.Root.Horizontal = v);
            applier.ApplyOr(def.Spacing, ValueParsers.Int, 8, scope, path.Field("Spacing"), v => hud.Inner.Root.Spacing = v);
            applier.ApplyOr(def.Alignment, ValueParsers.Align, UIAlign.Start, scope, path.Field("Alignment"), v => hud.Inner.Root.Alignment = v);

            // visibility: per-screen data visibility and ShowWhen
            ValueSource<bool>? showWhen = applier.Source(def.ShowWhen, ValueParsers.Bool, path.Field("ShowWhen"));
            hud.Visible = true;
            hud.ShowWhen = () =>
            {
                bool shown = runtime.Visible && (showWhen == null || showWhen.Get(scope));
                if (runtime.MarkShown(shown))
                {
                    runtime.Refresh(hud.Inner, opening: true);
                    hud.Inner.MarkLayoutDirty();
                }

                return shown;
            };
            hud.OnUpdate = DataBuilder.UpdateHandler<IUIHud>(def.OnUpdate, def.UpdateIntervalMs, scope, path.Field("UpdateIntervalMs"), applier);

            // tree
            builder.BuildTree(api, runtime, applier, hud.Inner.Root, def.Children, scope, path.Field("Children"));
            hud.Inner.DataRefresh = runtime.Refresh;

            if (def.Hotkey != null && ValueParsers.Keybind.Parse(def.Hotkey, out _))
            {
                api.RegisterHotkey(HotkeyId(runtime.Id), def.Hotkey, () => runtime.Visible = !runtime.Visible);
            }
            else
            {
                api.UnregisterHotkey(HotkeyId(runtime.Id));
            }
        }

        /// <summary>The id of a data HUD's toggle hotkey.</summary>
        internal static string HotkeyId(string hudId) => "hud:" + hudId;
    }
}
