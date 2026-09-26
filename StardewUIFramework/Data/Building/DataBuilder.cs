using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Bridge;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// Builds a data menu (or HUD tree) through the owner's <see cref="StardewUIApi"/> facade, so data gets the same
    /// id, sealing and registry checks as C# (parity by construction). Every value goes through the
    /// <see cref="PropertyApplier"/>; every event is an action list run by <see cref="DataActionRunner"/> inside the
    /// owner's callback guard; inputs read and write the state store (<c>Bind</c>). A failing element is reported with
    /// its path and skipped; its siblings still build. Structural elements (<c>If</c>, <c>Switch</c>, <c>With</c>) and
    /// output bindings live in <c>DataBuilder.Structure.cs</c>, events in <c>DataBuilder.Events.cs</c>.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        // defaults for construction arguments the C# API requires
        private const int DefaultSpacing = 8;
        private const int DefaultPanelPadding = 16;
        private const int DefaultViewportHeight = 300;
        private const float DefaultImageScale = 4f;
        private const double DefaultNumberMax = 999999;
        private const double DefaultSliderMax = 100;

        private readonly ExpressionValueResolver resolver;
        private readonly SpriteRefs sprites;
        private readonly Func<string, OwnerDefinition?> owners;
        private readonly DataStateStore store;

        internal DataBuilder(ExpressionValueResolver resolver, SpriteRefs sprites, Func<string, OwnerDefinition?> owners, DataStateStore store)
        {
            this.resolver = resolver;
            this.sprites = sprites;
            this.owners = owners;
            this.store = store;
        }

        /// <summary>
        /// What every element build needs: the facade, the applier of the current refresh group, the runtime and the id
        /// prefix of the subtree (row templates build under their row / cell container's id, never the item index).
        /// </summary>
        private sealed class BuildContext
        {
            internal BuildContext(StardewUIApi api, PropertyApplier applier, DataRuntime runtime, string prefix = "", OutletBinding? outlets = null, bool instanceState = false, int depth = 0)
            {
                Api = api;
                Applier = applier;
                Runtime = runtime;
                Prefix = prefix;
                Outlets = outlets;
                InstanceState = instanceState;
                Depth = depth;
            }

            internal StardewUIApi Api { get; }
            internal PropertyApplier Applier { get; }
            internal DataRuntime Runtime { get; }
            internal DataMessageLog Log => Applier.Log;

            /// <summary>Prepended to every element id built in this context (empty outside row templates and template / composite bodies).</summary>
            internal string Prefix { get; }

            /// <summary>The children of the template / composite instance whose body is being built (its Outlets place them), or null.</summary>
            internal OutletBinding? Outlets { get; }

            /// <summary>Inside a template / composite body: inputs without Bind keep their value per instance (<c>menu.&lt;prefixed id&gt;</c>).</summary>
            internal bool InstanceState { get; }

            /// <summary>Nesting depth of template / composite bodies (a template that expands itself stops at <see cref="MaxBodyDepth"/>).</summary>
            internal int Depth { get; }

            /// <summary>The runtime id of <paramref name="def"/> in this context.</summary>
            internal string IdOf(ElementDefinition def) => Prefix + def.Id;

            internal BuildContext WithGroup(RefresherGroup group) => new(Api, Applier.WithGroup(group), Runtime, Prefix, Outlets, InstanceState, Depth);

            /// <summary>This context building into <paramref name="group"/> with ids prefixed by <paramref name="containerId"/>.</summary>
            internal BuildContext ForTemplate(RefresherGroup group, string containerId) => new(Api, Applier.WithGroup(group), Runtime, containerId + ".", Outlets, InstanceState, Depth);

            /// <summary>This context building the body of template instance <paramref name="instanceId"/> (ids prefixed by it).</summary>
            internal BuildContext ForBody(string instanceId, OutletBinding outlets) => new(Api, Applier, Runtime, instanceId + ".", outlets, true, Depth + 1);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Menus
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Build (or re-build, inside <see cref="UIMenu.RebuildInPlace"/>) <paramref name="menu"/> from the runtime's definition.</summary>
        internal void BuildMenu(UIMenu menu, StardewUIApi api, DataMenuRuntime runtime)
        {
            runtime.ResetBuild();
            MenuDefinition def = runtime.Definition;
            DataPath path = runtime.Path;
            var applier = new PropertyApplier(resolver, runtime.Refreshers, runtime.Messages);
            DataScope scope = DataScope.ForRuntime(runtime, menu);
            runtime.Scope = scope;
            runtime.Lifetime = ParseLifetime(def.StateLifetime, path.Field("StateLifetime"), runtime.Messages);
            RegisterState(runtime, def.State, def.Computed, def.Watch, scope, path, runtime.Messages);
            RegisterSources(runtime, def.Sources);
            RegisterTemplates(runtime, def.Templates);
            IUIMenu m = menu;

            // options (reset to the defaults when absent, so a rebuild drops removed options)
            menu.TitleFunc = applier.Text(def.Title, scope, path.Field("Title"));
            applier.ApplyOr(def.Width, ValueParsers.OptionalInt, null, scope, path.Field("Width"), v => m.Width = v);
            applier.ApplyOr(def.Height, ValueParsers.OptionalInt, null, scope, path.Field("Height"), v => m.Height = v);
            applier.ApplyOr(def.ShowCloseButton, ValueParsers.Bool, true, scope, path.Field("ShowCloseButton"), v => m.ShowCloseButton = v);
            applier.ApplyOr(def.Modal, ValueParsers.Bool, true, scope, path.Field("Modal"), v => m.Modal = v);
            applier.ApplyOr(def.DimBackground, ValueParsers.Bool, true, scope, path.Field("DimBackground"), v => m.DimBackground = v);
            applier.ApplyOr(def.Anchor, ValueParsers.Anchor, UIAnchor.Center, scope, path.Field("Anchor"), v => m.Anchor = v);
            applier.ApplyOr(def.X, ValueParsers.Int, 0, scope, path.Field("X"), v => m.X = v);
            applier.ApplyOr(def.Y, ValueParsers.Int, 0, scope, path.Field("Y"), v => m.Y = v);
            applier.ApplyOr(def.DrawBox, ValueParsers.Bool, true, scope, path.Field("DrawBox"), v => m.DrawBox = v);
            applier.ApplyOr(def.Padding, ValueParsers.Int, 0, scope, path.Field("Padding"), v => m.Padding = v);
            applier.ApplyOr(def.CloseOnEscape, ValueParsers.Bool, true, scope, path.Field("CloseOnEscape"), v => m.CloseOnEscape = v);
            applier.ApplyOr(def.PlayerLayout, ValueParsers.Bool, true, scope, path.Field("PlayerLayout"), v => m.PlayerLayout = v);
            applier.ApplyOr(def.Resizable, ValueParsers.Bool, false, scope, path.Field("Resizable"), v => m.Resizable = v);

            // root stack
            applier.ApplyOr(def.Horizontal, ValueParsers.Bool, false, scope, path.Field("Horizontal"), v => menu.Root.Horizontal = v);
            applier.ApplyOr(def.Spacing, ValueParsers.Int, DefaultSpacing, scope, path.Field("Spacing"), v => menu.Root.Spacing = v);
            applier.ApplyOr(def.Alignment, ValueParsers.Align, UIAlign.Start, scope, path.Field("Alignment"), v => menu.Root.Alignment = v);

            // events
            menu.OnOpen = DataActionRunner.Handler<IUIMenu>(def.OnOpen, scope, "OnOpen");
            menu.OnClose = DataActionRunner.Handler<IUIMenu>(def.OnClose, scope, "OnClose");
            menu.OnScroll = DataActionRunner.Handler<int>(def.OnScroll, scope, "OnScroll");
            menu.OnUpdate = UpdateHandler<IUIMenu>(def.OnUpdate, def.UpdateIntervalMs, scope, path.Field("UpdateIntervalMs"), applier);
            menu.OnKey = KeyHandler(def.Keys, scope, path.Field("Keys"), runtime.Messages);

            // tree
            var context = new BuildContext(api, applier, runtime);
            BuildChildren(context, menu.Root, def.Children, scope, path.Field("Children"));

            ApplyExposures(api, menu, runtime, def, scope, path);
            menu.DefaultButtonElement = FindButton(menu, def.DefaultButton);
            menu.CancelButtonElement = FindButton(menu, def.CancelButton);

            // the data toggle goes through DataService.Toggle, so the entry's Condition and the wait-until-free queue
            // apply; its own id leaves a C# BindToggleHotkey on the same menu alone
            if (def.Hotkey != null && ValueParsers.Keybind.Parse(def.Hotkey, out _))
            {
                string key = runtime.Key;
                api.RegisterHotkey(MenuHotkeyId(runtime.MenuId), def.Hotkey, () =>
                {
                    if (UIServices.Data?.Toggle(key, out string error) == false)
                    {
                        UIServices.Log($"[{runtime.Owner}] the hotkey of menu '{key}' did nothing: {error}");
                    }
                });
            }
            else
            {
                api.UnregisterHotkey(MenuHotkeyId(runtime.MenuId));
            }

            menu.DataRefresh = runtime.Refresh;
        }

        /// <summary>The id of a data menu's toggle hotkey (<c>Hotkey</c>).</summary>
        internal static string MenuHotkeyId(string menuId) => "menu:" + menuId;

        /// <summary>Build a tree into <paramref name="root"/> (HUDs use this with the widget's root stack).</summary>
        internal void BuildTree(StardewUIApi api, DataRuntime runtime, PropertyApplier applier, IUIContainer root, List<ElementDefinition>? children, DataScope scope, DataPath path)
        {
            BuildChildren(new BuildContext(api, applier, runtime), root, children, scope, path);
        }

        private static Button? FindButton(UIMenu menu, string? id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : menu.Root.FindById(id.Trim()) as Button;
        }

        private static StateLifetime ParseLifetime(string? raw, DataPath path, DataMessageLog log)
        {
            if (raw == null)
            {
                return StateLifetime.Session;
            }

            if (Enum.TryParse(raw.Trim(), ignoreCase: true, out StateLifetime lifetime) && Enum.IsDefined(lifetime))
            {
                return lifetime;
            }

            log.Warn(path, $"'{raw}' is not Session or Open; Session is used.");
            return StateLifetime.Session;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  State, computed values and watches
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Register the UI's <c>State</c> defaults (only filling missing values), <c>Computed</c> values and <c>Watch</c>es.</summary>
        internal void RegisterState(DataRuntime runtime, Dictionary<string, string>? state, Dictionary<string, string>? computed, Dictionary<string, List<ActionDefinition>>? watch, DataScope scope, DataPath path, DataMessageLog log)
        {
            if (state != null)
            {
                foreach ((string rawName, string? raw) in state)
                {
                    string name = rawName.Trim();
                    if (name.Length == 0)
                    {
                        continue;
                    }

                    var address = new StateAddress(StateScope.Menu, runtime.StateKey, name);
                    store.SetDefault(address, DefaultFactory(raw, scope));
                }
            }

            if (computed != null)
            {
                foreach ((string rawName, string? raw) in computed)
                {
                    CompiledExpression? expression = CompileBare(raw, path.Field("Computed").Field(rawName), log);
                    if (expression != null)
                    {
                        runtime.AddComputed(rawName.Trim(), expression);
                    }
                }
            }

            if (watch != null)
            {
                foreach ((string key, List<ActionDefinition>? actions) in watch)
                {
                    if (actions == null)
                    {
                        continue;
                    }

                    if (StateAddress.TryParse(key, scope, allowBare: true, out StateAddress address, out string error))
                    {
                        runtime.AddWatch(address, actions);
                    }
                    else
                    {
                        log.Error(path.Field("Watch").Field(key), error);
                    }
                }
            }
        }

        /// <summary>Remember the UI's named row sources (<c>Sources</c>); collections resolve them by name.</summary>
        internal static void RegisterSources(DataRuntime runtime, Dictionary<string, SourceDefinition>? sources)
        {
            if (sources == null)
            {
                return;
            }

            foreach ((string name, SourceDefinition? source) in sources)
            {
                if (source != null && !string.IsNullOrWhiteSpace(name))
                {
                    runtime.Sources[name.Trim()] = source;
                }
            }
        }

        /// <summary>A default factory for a <c>State</c> value: literals are inferred (numbers, true / false, text); templates are evaluated when the value is created.</summary>
        private static Func<DataValue> DefaultFactory(string? raw, DataScope scope)
        {
            if (raw == null)
            {
                return () => DataValue.Null;
            }

            if (!ExpressionValueResolver.HasTemplate(raw))
            {
                DataValue literal = StateAddress.Infer(raw.Replace("$${", "${", StringComparison.Ordinal));
                return () => literal;
            }

            Template template = Template.Parse(raw);
            return () => template.Evaluate(scope, ExpressionValueResolver.Functions).Value;
        }

        /// <summary>Compile a bare expression field (<c>Computed</c>, <c>If</c>...); a single <c>${...}</c> is accepted too.</summary>
        private static CompiledExpression? CompileBare(string? raw, DataPath path, DataMessageLog log)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                log.Warn(path, "the expression is empty; it is ignored.");
                return null;
            }

            if (ExpressionValueResolver.HasTemplate(raw))
            {
                Template template = Template.Parse(raw);
                if (template.IsSingleExpression && template.Segments[0].Expression is { } single && !template.Segments[0].IsOneTime)
                {
                    raw = single.Source;
                }
                else
                {
                    log.Error(path, $"'{raw}' must be one expression (write text as a string, e.g. 'Day ' + game.day).");
                    return null;
                }
            }

            CompiledExpression expression = CompiledExpression.Compile(raw);
            if (expression.Error != null)
            {
                log.Error(path, $"expression error in '{raw}': {expression.Error}");
                return null;
            }

            return expression;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Elements
        // ---------------------------------------------------------------------------------------------------------

        private void BuildChildren(BuildContext ctx, IUIContainer parent, List<ElementDefinition>? children, DataScope scope, DataPath path)
        {
            if (children == null)
            {
                return;
            }

            for (int i = 0; i < children.Count; i++)
            {
                ElementDefinition? child = children[i];
                if (child?.Type == null || child.Id == null)
                {
                    continue; // reported by the validator
                }

                BuildOne(ctx, parent, child, scope, path.Index(i, child.Id));
            }
        }

        /// <summary>Build one element (with its <c>If</c>) at the end of <paramref name="parent"/>; a failure is logged with its path.</summary>
        private void BuildOne(BuildContext ctx, IUIContainer parent, ElementDefinition child, DataScope scope, DataPath childPath)
        {
            try
            {
                if (child.If != null)
                {
                    BuildIf(ctx, parent, child, scope, childPath);
                }
                else
                {
                    BuildElement(ctx, parent, child, scope, childPath);
                }
            }
            catch (Exception ex)
            {
                ctx.Log.Error(childPath, $"could not be built: {ex.Message}");
            }
        }

        /// <summary>Create one element (and its subtree) at the end of <paramref name="parent"/>; <c>If</c> is not checked here.</summary>
        private UIElement BuildElement(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope parentScope, DataPath path)
        {
            DataScope baseScope = parentScope;
            if (def.With != null)
            {
                PathSegment[]? with = parentScope.ResolveWith(def.With, out string withError);
                if (with != null)
                {
                    baseScope = parentScope.WithRelative(with);
                }
                else
                {
                    ctx.Log.Error(path.Field("With"), $"'{def.With}': {withError}");
                }
            }

            IUIElement element = def.Type == ElementTypes.Switch
                ? ctx.Api.AddStack(parent, ctx.IdOf(def), false, 0)
                : Create(ctx, parent, def, baseScope, path);
            UIElement internalElement = (UIElement)element;
            DataScope scope = baseScope.WithElement(internalElement);

            var visibility = new Visibility(internalElement);
            ApplyTypeMembers(element, def, scope, path, ctx.Applier);
            ApplyCommon(ctx, element, def, scope, path, visibility);
            WireEvents(ctx, element, def, scope, path);

            if (def.Condition != null)
            {
                ctx.Applier.AddRefresher(new ConditionRefresher(visibility, def.Condition));
            }

            if (def.Out != null)
            {
                AddOutputs(ctx, internalElement, def.Out, scope, path.Field("Out"));
            }

            if (def.Type == ElementTypes.Composite)
            {
                FinishComposite((Composite)internalElement, def, baseScope, scope, path);
            }
            else if (def.Type == ElementTypes.Template)
            {
                // the instance's children went into the body's Outlets (CreateTemplate)
            }
            else if (def.Type == ElementTypes.Outlet)
            {
                BuildOutlet(ctx, (UIContainer)internalElement, def, baseScope, path);
            }
            else if (def.Type == ElementTypes.Switch)
            {
                BuildSwitch(ctx, (UIContainer)internalElement, def, baseScope, path);
            }
            else if (def.Type == ElementTypes.Repeat)
            {
                BuildRepeat(ctx, (UIContainer)internalElement, def, baseScope, path);
            }
            else if (element is IUIContainer container && ElementTypes.IsContainer(def.Type!))
            {
                BuildChildren(ctx, container, def.Children, baseScope, path.Field("Children"));
            }

            return internalElement;
        }

        /// <summary>Create the element through the facade with its construction arguments.</summary>
        private IUIElement Create(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            StardewUIApi api = ctx.Api;
            PropertyApplier applier = ctx.Applier;
            string id = ctx.IdOf(def);
            switch (def.Type)
            {
                case ElementTypes.Stack:
                case ElementTypes.Repeat:
                    return api.AddStack(parent, id, false, DefaultSpacing);
                case ElementTypes.List:
                    return CreateList(ctx, parent, def, scope, path);
                case ElementTypes.DataGrid:
                    return CreateDataGrid(ctx, parent, def, scope, path);
                case ElementTypes.Form:
                    return def.Model != null ? CreateModelForm(ctx, parent, def, scope, path) : CreateForm(ctx, parent, def, scope, path);
                case ElementTypes.Composite:
                    return CreateComposite(ctx, parent, def, scope, path);
                case ElementTypes.Template:
                    return CreateTemplate(ctx, parent, def, scope, path);
                case ElementTypes.Outlet:
                    return api.AddStack(parent, id, false, DefaultSpacing);
                case ElementTypes.Grid:
                    return api.AddGrid(parent, id, "*", def.Rows ?? "auto"); // the column tracks are applied (possibly live) with the type members
                case ElementTypes.Panel:
                    return api.AddPanel(parent, id, true, DefaultPanelPadding);
                case ElementTypes.Canvas:
                    return api.AddCanvas(parent, id);
                case ElementTypes.ScrollView:
                    return api.AddScrollView(parent, id, DefaultViewportHeight);
                case ElementTypes.Slot:
                    return api.AddSlot(parent, id);
                case ElementTypes.Spacer:
                    return api.AddSpacer(parent, id, 0, 0);
                case ElementTypes.Label:
                    return api.AddLabel(parent, id, applier.Text(def.Text, scope, path.Field("Text")) ?? (() => string.Empty));
                case ElementTypes.Button:
                    return api.AddButton(parent, id, applier.Text(def.Text, scope, path.Field("Text")) ?? (() => string.Empty), null!);
                case ElementTypes.Image:
                    return CreateImage(api, parent, id, def, scope, path, applier);
                case ElementTypes.ItemImage:
                    return CreateItemImage(api, parent, id, def, scope, path, applier);
                case ElementTypes.Checkbox:
                {
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Bool, false, DataValue.FromBool);
                    return api.AddCheckbox(parent, id, () => target.Read().AsBool(), v => Write(scope, target, DataValue.FromBool(v)));
                }
                case ElementTypes.TextInput:
                {
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Text, string.Empty, DataValue.FromString);
                    return api.AddTextInput(parent, id, () => target.Read().AsString(), v => Write(scope, target, DataValue.FromString(v ?? string.Empty)));
                }
                case ElementTypes.NumberInput:
                {
                    double min = applier.Initial(def.Min, ValueParsers.Number, 0, scope, path.Field("Min"));
                    double max = applier.Initial(def.Max, ValueParsers.Number, DefaultNumberMax, scope, path.Field("Max"));
                    double step = applier.Initial(def.Step, ValueParsers.Number, 1, scope, path.Field("Step"));
                    bool clamp = applier.Initial(def.Clamp, ValueParsers.Bool, true, scope, path.Field("Clamp"));
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Number, min, DataValue.FromNumber);
                    return api.AddNumberInput(parent, id, () => target.Read().AsNumber(), v => Write(scope, target, DataValue.FromNumber(v)), min, max, step, clamp);
                }
                case ElementTypes.Dropdown:
                {
                    if (def.ChoicesSource != null)
                    {
                        return CreateSourcedDropdown(ctx, parent, def, scope, path);
                    }

                    string[] choices = def.Choices?.ToArray() ?? Array.Empty<string>();
                    string[]? labels = def.Labels == null ? null : LabelsFor(choices, def.Labels);
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Text, choices.Length > 0 ? choices[0] : string.Empty, DataValue.FromString);
                    return api.AddDropdown(parent, id, () => choices, labels == null ? null! : () => labels, () => target.Read().AsString(), v => Write(scope, target, DataValue.FromString(v ?? string.Empty)));
                }
                case ElementTypes.Slider:
                {
                    double min = applier.Initial(def.Min, ValueParsers.Number, 0, scope, path.Field("Min"));
                    double max = applier.Initial(def.Max, ValueParsers.Number, DefaultSliderMax, scope, path.Field("Max"));
                    BindTarget target = BindInput(ctx, def, scope, path, ValueParsers.Number, min, DataValue.FromNumber);
                    return api.AddSlider(parent, id, () => target.Read().AsNumber(), v => Write(scope, target, DataValue.FromNumber(v)), min, max);
                }
                default:
                    throw new InvalidOperationException($"unsupported element type '{def.Type}'.");
            }
        }

        /// <summary>
        /// The value an input reads and writes: its <c>Bind</c> (default <c>menu.&lt;Id&gt;</c>), a state value or (v1.6)
        /// a member of a model exposed from C# (<c>model.settings.Day</c>). For state values the input's <c>Value</c>
        /// becomes the value's default (only used while the value does not exist); without one, the type's fallback is
        /// the default unless another default (the menu's <c>State</c>) is already registered. A model provides its own value.
        /// </summary>
        private BindTarget BindInput<T>(BuildContext ctx, ElementDefinition def, DataScope scope, DataPath path, ValueKind<T> kind, T fallback, Func<T, DataValue> wrap)
        {
            StateAddress address = new(StateScope.Menu, scope.StateKey ?? scope.MenuKey, ctx.InstanceState ? ctx.IdOf(def) : def.Id!);
            if (def.Bind != null)
            {
                if (BindTarget.TryParse(def.Bind, scope, allowBare: true, out BindTarget? bound, out string error))
                {
                    if (!bound.IsState)
                    {
                        if (def.Value != null)
                        {
                            ctx.Log.Info(path.Field("Value"), $"the input is bound to {bound}, which provides its own value; Value is ignored.");
                        }

                        return bound;
                    }

                    address = bound.Address;
                }
                else
                {
                    ctx.Log.Error(path.Field("Bind"), $"{error} The input uses {address} instead.");
                }
            }

            ValueSource<T>? initial = def.Value == null ? null : ctx.Applier.Source(def.Value, kind, path.Field("Value"));
            if (initial != null || !store.HasDefault(address))
            {
                store.SetDefault(address, () => wrap(initial == null ? fallback : initial.Get(scope)));
            }

            return BindTarget.ForState(address);
        }

        /// <summary>Write an input's value into the state store or model (a failed write, e.g. <c>player.*</c> on the title screen, is logged).</summary>
        private static void Write(DataScope scope, BindTarget target, DataValue value)
        {
            if (!target.Write(value, out string error))
            {
                UIServices.Log($"[{scope.Owner}] {scope}: could not write {target}: {error}", StardewModdingAPI.LogLevel.Warn);
            }
        }

        /// <summary>Labels padded with the choice values so both lists have the same length.</summary>
        private static string[] LabelsFor(string[] choices, List<string> labels)
        {
            var result = new string[choices.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                result[i] = i < labels.Count ? labels[i] : choices[i];
            }

            return result;
        }

        /// <summary>
        /// An image whose <c>Sprite</c> is a value like any other field: a literal reference, or a live <c>${...}</c>
        /// giving a reference string, a <see cref="Texture2D"/> or a <c>Tuple&lt;Texture2D, Rectangle&gt;</c> (a C# row's
        /// sprite), so a row template can show a different image per row. The sprite's own source / scale / tint apply
        /// unless the element sets <c>Source</c> / <c>Scale</c> / <c>Tint</c> itself.
        /// </summary>
        private IUIElement CreateImage(StardewUIApi api, IUIContainer parent, string id, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier applier)
        {
            IUIImage image = api.AddImage(parent, id, null!, null, DefaultImageScale);
            if (string.IsNullOrWhiteSpace(def.Sprite))
            {
                applier.Log.Warn(path.Field("Sprite"), "an Image needs a Sprite (sprite:Owner/name, item:(O)24 or asset:Path@x,y,w,h); it draws nothing.");
                return image;
            }

            DataPath spritePath = path.Field("Sprite");
            bool ownSource = def.Source?.Shorthand != null, ownScale = def.Scale != null, ownTint = def.Tint != null;
            string? lastText = null;
            applier.Apply(def.Sprite.Trim(), ValueParsers.Raw, scope, spritePath, value =>
            {
                string? text = value.Kind == DataKind.String ? value.AsString() : null;
                if (text != null && text == lastText)
                {
                    return; // same reference: already applied
                }

                lastText = text;
                if (!TryResolveSprite(value, scope.Owner, out SpriteRef sprite, out string error))
                {
                    applier.Log.Error(spritePath, error);
                    return;
                }

                image.Texture = sprite.Texture!;
                if (!ownSource)
                {
                    image.Source = sprite.Source;
                }

                if (!ownScale && sprite.Scale.HasValue)
                {
                    image.Scale = sprite.Scale.Value;
                }

                if (!ownTint && sprite.Tint.HasValue)
                {
                    image.Tint = sprite.Tint.Value;
                }
            });

            return image;
        }

        /// <summary>A sprite value: a reference string (<see cref="SpriteRefs"/>), a texture, or a (texture, source) tuple.</summary>
        private bool TryResolveSprite(DataValue value, string owner, out SpriteRef sprite, out string error)
        {
            error = string.Empty;
            switch (value.AsObject())
            {
                case Texture2D texture:
                    sprite = new SpriteRef(texture, null, null, null, null);
                    return true;
                case Tuple<Texture2D, Rectangle> pair:
                    sprite = new SpriteRef(pair.Item1, pair.Item2, null, null, null);
                    return true;
                case ValueTuple<Texture2D, Rectangle> pair:
                    sprite = new SpriteRef(pair.Item1, pair.Item2, null, null, null);
                    return true;
                default:
                    if (value.IsNull || value.AsString().Trim().Length == 0)
                    {
                        sprite = default;
                        error = "the Sprite value is empty.";
                        return false;
                    }

                    return sprites.TryResolve(value.AsString(), owner, out sprite, out error);
            }
        }

        /// <summary>
        /// An item image whose <c>Item</c> is a qualified item id, an item query (<c>FLAVORED_ITEM Wine (O)398</c>) or an
        /// expression giving an item (<c>${row.item}</c>) or item text. Items created from text are cached by
        /// (text, count, quality), so a live value that keeps giving the same text never re-creates the item.
        /// </summary>
        private static IUIElement CreateItemImage(StardewUIApi api, IUIContainer parent, string id, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier applier)
        {
            DataValue value = DataValue.Null;
            int count = 1;
            int quality = 0;
            applier.Apply(def.Count, ValueParsers.Int, scope, path.Field("Count"), v => count = Math.Max(1, v));
            applier.Apply(def.Quality, ValueParsers.Int, scope, path.Field("Quality"), v => quality = Math.Clamp(v, 0, 4));

            // the getter goes through the item cache (a dictionary hit once created), so an item that cannot be created
            // yet (an item query on the title screen) is retried instead of staying empty
            IUIItemImage image = api.AddItemImage(parent, id, () => RowScope.ItemOf(value, count, quality)!, DefaultImageScale);
            if (!string.IsNullOrWhiteSpace(def.Item))
            {
                applier.Apply(def.Item.Trim(), ValueParsers.Raw, scope, path.Field("Item"), v => value = v);
            }

            return image;
        }

        /// <summary>The members that only some element types have (set through the public interfaces).</summary>
        private void ApplyTypeMembers(IUIElement element, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            switch (element)
            {
                case IUIList list:
                    a.Apply(def.RowHeight, ValueParsers.Int, scope, path.Field("RowHeight"), v => list.RowHeight = v);
                    a.Apply(def.VisibleRows, ValueParsers.Int, scope, path.Field("VisibleRows"), v => list.VisibleRows = v);
                    a.Apply(def.Selectable, ValueParsers.Bool, scope, path.Field("Selectable"), v => list.Selectable = v);
                    break;
                case IUIDataGrid grid:
                    a.Apply(def.RowHeight, ValueParsers.Int, scope, path.Field("RowHeight"), v => grid.RowHeight = v);
                    a.Apply(def.VisibleRows, ValueParsers.Int, scope, path.Field("VisibleRows"), v => grid.VisibleRows = v);
                    a.Apply(def.Selectable, ValueParsers.Bool, scope, path.Field("Selectable"), v => grid.Selectable = v);
                    a.Apply(def.MultiSelect, ValueParsers.Bool, scope, path.Field("MultiSelect"), v => grid.MultiSelect = v);
                    a.Apply(def.ScrollSound, ValueParsers.Text, scope, path.Field("ScrollSound"), v => grid.ScrollSound = v);
                    a.Apply(def.SelectSound, ValueParsers.Text, scope, path.Field("SelectSound"), v => grid.SelectSound = v);
                    a.Apply(def.SortSound, ValueParsers.Text, scope, path.Field("SortSound"), v => grid.SortSound = v);
                    break;
                case IUIForm form:
                    a.Apply(def.ShowButtons, ValueParsers.Bool, scope, path.Field("ShowButtons"), v => form.ShowButtons = v);
                    break;
                case IUISlot slot:
                    a.Apply(def.Horizontal, ValueParsers.Bool, scope, path.Field("Horizontal"), v => slot.Horizontal = v);
                    a.Apply(def.MaxHeight, ValueParsers.OptionalInt, scope, path.Field("MaxHeight"), v => slot.MaxHeight = v);
                    a.Apply(def.MaxContributions, ValueParsers.Int, scope, path.Field("MaxContributions"), v => slot.MaxContributions = v);
                    a.Apply(def.Wrap, ValueParsers.Bool, scope, path.Field("Wrap"), v => slot.Wrap = v);
                    break;
                case IUIStack stack:
                    a.Apply(def.Horizontal, ValueParsers.Bool, scope, path.Field("Horizontal"), v => stack.Horizontal = v);
                    a.Apply(def.Spacing, ValueParsers.Int, scope, path.Field("Spacing"), v => stack.Spacing = v);
                    a.Apply(def.Alignment, ValueParsers.Align, scope, path.Field("Alignment"), v => stack.Alignment = v);
                    a.Apply(def.Wrap, ValueParsers.Bool, scope, path.Field("Wrap"), v => stack.Wrap = v);
                    break;
                case IUIGrid grid:
                    a.Apply(GridTracks(def.Columns), ValueParsers.Text, scope, path.Field("Columns"), v => grid.Columns = v);
                    a.Apply(def.Rows, ValueParsers.Text, scope, path.Field("Rows"), v => grid.Rows = v);
                    a.Apply(def.ColumnSpacing, ValueParsers.Int, scope, path.Field("ColumnSpacing"), v => grid.ColumnSpacing = v);
                    a.Apply(def.RowSpacing, ValueParsers.Int, scope, path.Field("RowSpacing"), v => grid.RowSpacing = v);
                    break;
                case IUIPanel panel:
                    a.Apply(def.DrawBox, ValueParsers.Bool, scope, path.Field("DrawBox"), v => panel.DrawBox = v);
                    a.Apply(def.Padding, ValueParsers.Int, scope, path.Field("Padding"), v => panel.Padding = v);
                    break;
                case IUIScrollView scroll:
                    a.Apply(def.ViewportHeight, ValueParsers.Int, scope, path.Field("ViewportHeight"), v => scroll.ViewportHeight = v);
                    a.Apply(def.ScrollStep, ValueParsers.Int, scope, path.Field("ScrollStep"), v => scroll.ScrollStep = v);
                    a.Apply(def.ShowScrollbar, ValueParsers.Bool, scope, path.Field("ShowScrollbar"), v => scroll.ShowScrollbar = v);
                    break;
                case IUISpacer spacer:
                    a.Apply(def.Line, ValueParsers.Bool, scope, path.Field("Line"), v => spacer.Line = v);
                    break;
                case IUILabel label:
                    a.Apply(def.Font, ValueParsers.Font, scope, path.Field("Font"), v => label.Font = v);
                    a.Apply(def.Color, ValueParsers.ColorValue, scope, path.Field("Color"), v => label.Color = v);
                    a.Apply(def.Shadow, ValueParsers.Bool, scope, path.Field("Shadow"), v => label.Shadow = v);
                    a.Apply(def.Wrap, ValueParsers.Bool, scope, path.Field("Wrap"), v => label.Wrap = v);
                    a.Apply(def.TextAlign, ValueParsers.Align, scope, path.Field("TextAlign"), v => label.TextAlign = v);
                    a.Apply(def.Scale, ValueParsers.Float, scope, path.Field("Scale"), v => label.Scale = v);
                    a.Apply(def.RichText, ValueParsers.Bool, scope, path.Field("RichText"), v => label.RichText = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => label.Shrink = v);
                    break;
                case IUIButton button:
                    a.Apply(def.Font, ValueParsers.Font, scope, path.Field("Font"), v => button.Font = v);
                    ApplyIcon(button, def, scope, path, a);
                    a.Apply(def.IconScale, ValueParsers.Float, scope, path.Field("IconScale"), v => button.IconScale = v);
                    a.Apply(def.ClickSound, ValueParsers.Text, scope, path.Field("ClickSound"), v => button.ClickSound = v);
                    a.Apply(def.HoverSound, ValueParsers.Text, scope, path.Field("HoverSound"), v => button.HoverSound = v);
                    a.Apply(def.DrawBox, ValueParsers.Bool, scope, path.Field("DrawBox"), v => button.DrawBox = v);
                    a.Apply(def.RichText, ValueParsers.Bool, scope, path.Field("RichText"), v => button.RichText = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => button.Shrink = v);
                    break;
                case IUIImage image:
                    a.Apply(def.Source?.Shorthand, ValueParsers.RectangleValue, scope, path.Field("Source"), v => image.Source = v);
                    a.Apply(def.Scale, ValueParsers.Float, scope, path.Field("Scale"), v => image.Scale = v);
                    a.Apply(def.Tint, ValueParsers.ColorValue, scope, path.Field("Tint"), v => image.Tint = v);
                    break;
                case IUIItemImage itemImage:
                    a.Apply(def.Scale, ValueParsers.Float, scope, path.Field("Scale"), v => itemImage.Scale = v);
                    a.Apply(def.Stack, ValueParsers.ItemStack, scope, path.Field("Stack"), v => itemImage.Stack = v);
                    a.Apply(def.DrawShadow, ValueParsers.Bool, scope, path.Field("DrawShadow"), v => itemImage.DrawShadow = v);
                    a.Apply(def.Alpha, ValueParsers.Float, scope, path.Field("Alpha"), v => itemImage.Alpha = v);
                    a.Apply(def.Tint, ValueParsers.ColorValue, scope, path.Field("Tint"), v => itemImage.Tint = v);
                    break;
                case IUICheckbox checkbox:
                    Func<string>? checkboxLabel = a.Text(def.Label, scope, path.Field("Label"));
                    if (checkboxLabel != null)
                    {
                        checkbox.Label = checkboxLabel;
                    }

                    a.Apply(def.ClickSound, ValueParsers.Text, scope, path.Field("ClickSound"), v => checkbox.ClickSound = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => checkbox.Shrink = v);
                    break;
                case IUITextInput text:
                    Func<string>? placeholder = a.Text(def.Placeholder, scope, path.Field("Placeholder"));
                    if (placeholder != null)
                    {
                        text.Placeholder = placeholder;
                    }

                    a.Apply(def.MaxLength, ValueParsers.Int, scope, path.Field("MaxLength"), v => text.MaxLength = v);
                    ApplyTexture(def.Texture, scope, path.Field("Texture"), a, t => text.Texture = t);
                    break;
                case IUINumberInput number:
                    // Min / Max / Step / Clamp were passed to the constructor; live values are re-applied here
                    a.Apply(def.Min, ValueParsers.Number, scope, path.Field("Min"), v => number.Min = v);
                    a.Apply(def.Max, ValueParsers.Number, scope, path.Field("Max"), v => number.Max = v);
                    a.Apply(def.Step, ValueParsers.Number, scope, path.Field("Step"), v => number.Step = v);
                    a.Apply(def.Clamp, ValueParsers.Bool, scope, path.Field("Clamp"), v => number.Clamp = v);
                    a.Apply(def.Decimals, ValueParsers.Int, scope, path.Field("Decimals"), v => number.Decimals = v);
                    ApplyTexture(def.Texture, scope, path.Field("Texture"), a, t => number.Texture = t);
                    break;
                case IUIDropdown dropdown:
                    a.Apply(def.MaxVisible, ValueParsers.Int, scope, path.Field("MaxVisible"), v => dropdown.MaxVisible = v);
                    a.Apply(def.Shrink, ValueParsers.Bool, scope, path.Field("Shrink"), v => dropdown.Shrink = v);
                    break;
                case IUISlider slider:
                    a.Apply(def.Min, ValueParsers.Number, scope, path.Field("Min"), v => slider.Min = v);
                    a.Apply(def.Max, ValueParsers.Number, scope, path.Field("Max"), v => slider.Max = v);
                    a.Apply(def.Step, ValueParsers.Number, scope, path.Field("Step"), v => slider.Step = v);
                    break;
            }
        }

        /// <summary>A Grid's column tracks as the grid parses them ("auto, *, 120"): the widths of its column list.</summary>
        private static string? GridTracks(List<ColumnDefinition>? columns)
        {
            if (columns == null || columns.Count == 0)
            {
                return null;
            }

            return string.Join(", ", columns.Select(c => string.IsNullOrWhiteSpace(c?.Width) ? "*" : c!.Width!.Trim()));
        }

        private void ApplyIcon(IUIButton button, ElementDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            a.Apply(def.Icon, ValueParsers.Text, scope, path.Field("Icon"), reference =>
            {
                if (!sprites.TryResolve(reference, scope.Owner, out SpriteRef sprite, out string error))
                {
                    a.Log.Error(path.Field("Icon"), error);
                    return;
                }

                button.Icon = sprite.Texture!;
                button.IconSource = sprite.Source;
                if (sprite.Scale.HasValue)
                {
                    button.IconScale = sprite.Scale.Value;
                }
            });
        }

        private void ApplyTexture(string? raw, DataScope scope, DataPath path, PropertyApplier a, Action<Microsoft.Xna.Framework.Graphics.Texture2D> apply)
        {
            a.Apply(raw, ValueParsers.Text, scope, path, reference =>
            {
                if (sprites.TryResolve(reference, scope.Owner, out SpriteRef sprite, out string error))
                {
                    apply(sprite.Texture!);
                }
                else
                {
                    a.Log.Error(path, error);
                }
            });
        }

        /// <summary>Members every element has.</summary>
        private void ApplyCommon(BuildContext ctx, IUIElement e, ElementDefinition def, DataScope scope, DataPath path, Visibility visibility)
        {
            PropertyApplier a = ctx.Applier;
            a.Apply(def.Visible, ValueParsers.Bool, scope, path.Field("Visible"), v => visibility.SetRequested(v));
            a.Apply(def.Enabled, ValueParsers.Bool, scope, path.Field("Enabled"), v => e.Enabled = v);
            Func<string>? tooltip = a.Text(def.Tooltip, scope, path.Field("Tooltip"));
            if (tooltip != null)
            {
                e.Tooltip = tooltip;
            }

            Func<string>? tooltipTitle = a.Text(def.TooltipTitle, scope, path.Field("TooltipTitle"));
            if (tooltipTitle != null)
            {
                e.TooltipTitle = tooltipTitle;
            }

            if (def.RichTooltip != null)
            {
                CompiledTooltip? rich = CompileTooltip(def.RichTooltip, scope.Owner, path.Field("RichTooltip"), a);
                if (rich != null)
                {
                    e.RichTooltip = rich.Create(scope);
                }
            }

            a.Apply(def.Tag, ValueParsers.Text, scope, path.Field("Tag"), v => e.Tag = v);
            a.Apply(def.Sealed, ValueParsers.Bool, scope, path.Field("Sealed"), v => e.Sealed = v);
            Func<string>? accessibleName = a.Text(def.AccessibleName, scope, path.Field("AccessibleName"));
            if (accessibleName != null)
            {
                e.AccessibleName = accessibleName;
            }

            a.Apply(def.Margin, ValueParsers.Margin, scope, path.Field("Margin"), v => e.SetMargin(v[0], v[1], v[2], v[3]));
            a.Apply(def.MarginLeft, ValueParsers.Int, scope, path.Field("MarginLeft"), v => e.MarginLeft = v);
            a.Apply(def.MarginTop, ValueParsers.Int, scope, path.Field("MarginTop"), v => e.MarginTop = v);
            a.Apply(def.MarginRight, ValueParsers.Int, scope, path.Field("MarginRight"), v => e.MarginRight = v);
            a.Apply(def.MarginBottom, ValueParsers.Int, scope, path.Field("MarginBottom"), v => e.MarginBottom = v);
            a.Apply(def.Width, ValueParsers.OptionalInt, scope, path.Field("Width"), v => e.Width = v);
            a.Apply(def.Height, ValueParsers.OptionalInt, scope, path.Field("Height"), v => e.Height = v);
            a.Apply(def.MinWidth, ValueParsers.OptionalInt, scope, path.Field("MinWidth"), v => e.MinWidth = v);
            a.Apply(def.MaxWidth, ValueParsers.OptionalInt, scope, path.Field("MaxWidth"), v => e.MaxWidth = v);
            a.Apply(def.HorizontalAlign, ValueParsers.Align, scope, path.Field("HorizontalAlign"), v => e.HorizontalAlign = v);
            a.Apply(def.VerticalAlign, ValueParsers.Align, scope, path.Field("VerticalAlign"), v => e.VerticalAlign = v);
            a.Apply(def.X, ValueParsers.Int, scope, path.Field("X"), v => e.X = v);
            a.Apply(def.Y, ValueParsers.Int, scope, path.Field("Y"), v => e.Y = v);
            a.Apply(def.Cell, ValueParsers.Pair, scope, path.Field("Cell"), v =>
            {
                e.Row = v.X;
                e.Column = v.Y;
            });
            a.Apply(def.Span, ValueParsers.Pair, scope, path.Field("Span"), v =>
            {
                e.RowSpan = v.X;
                e.ColumnSpan = v.Y;
            });
            a.Apply(def.Row, ValueParsers.Int, scope, path.Field("Row"), v => e.Row = v);
            a.Apply(def.Column, ValueParsers.Int, scope, path.Field("Column"), v => e.Column = v);
            a.Apply(def.RowSpan, ValueParsers.Int, scope, path.Field("RowSpan"), v => e.RowSpan = v);
            a.Apply(def.ColumnSpan, ValueParsers.Int, scope, path.Field("ColumnSpan"), v => e.ColumnSpan = v);

            ApplyDrawHooks(e, def, scope, path, a.Log);

            StyleDefinition? style = EffectiveStyle(def, scope.Owner, path, a.Log);
            if (style != null)
            {
                e.Style = BuildStyle(ctx.Api, style, scope, path.Field("Style"), a);
            }
        }

        /// <summary>The element's style: its <c>Class</c>es (from the owner's <c>Owners</c> entry) merged in order, then the inline <c>Style</c>.</summary>
        private StyleDefinition? EffectiveStyle(ElementDefinition def, string owner, DataPath path, DataMessageLog log)
        {
            if (string.IsNullOrWhiteSpace(def.Class))
            {
                return def.Style;
            }

            Dictionary<string, StyleDefinition>? classes = owners(owner)?.Classes;
            StyleDefinition merged = new();
            foreach (string name in def.Class.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                StyleDefinition? found = classes?.FirstOrDefault(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
                if (found == null)
                {
                    string? suggestion = classes == null ? null : DataValidator.Suggest(name, classes.Keys);
                    log.Warn(path.Field("Class"), $"'{owner}' has no style class '{name}' in {DataAssets.Owners}{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}.");
                    continue;
                }

                MergeStyle(merged, found);
            }

            if (def.Style != null)
            {
                MergeStyle(merged, def.Style);
            }

            return merged;
        }

        /// <summary>Copy every member <paramref name="over"/> sets onto <paramref name="target"/>.</summary>
        internal static void MergeStyle(StyleDefinition target, StyleDefinition over)
        {
            foreach (System.Reflection.PropertyInfo property in DataValidator.ModelProperties(typeof(StyleDefinition)))
            {
                object? value = property.GetValue(over);
                if (value != null)
                {
                    property.SetValue(target, value);
                }
            }
        }

        /// <summary>Build an <see cref="IUIStyle"/> from a style definition (also used for the owner's DefaultStyle).</summary>
        internal IUIStyle BuildStyle(StardewUIApi api, StyleDefinition def, DataScope scope, DataPath path, PropertyApplier a)
        {
            IUIStyle style = api.CreateStyle();
            a.Apply(def.Font, ValueParsers.Font, scope, path.Field("Font"), v => style.Font = v);
            a.Apply(def.TextColor, ValueParsers.ColorValue, scope, path.Field("TextColor"), v => style.TextColor = v);
            a.Apply(def.HoverColor, ValueParsers.ColorValue, scope, path.Field("HoverColor"), v => style.HoverColor = v);
            ApplyTexture(def.BoxTexture, scope, path.Field("BoxTexture"), a, t => style.BoxTexture = t);
            if (def.BoxTexture != null && def.BoxSource == null && sprites.TryResolve(def.BoxTexture, scope.Owner, out SpriteRef sprite, out _))
            {
                style.BoxSource = sprite.BoxSource ?? sprite.Source;
            }

            a.Apply(def.BoxSource, ValueParsers.RectangleValue, scope, path.Field("BoxSource"), v => style.BoxSource = v);
            a.Apply(def.BoxScale, ValueParsers.Float, scope, path.Field("BoxScale"), v => style.BoxScale = v);
            a.Apply(def.Padding, ValueParsers.Int, scope, path.Field("Padding"), v => style.Padding = v);
            a.Apply(def.TextShadow, ValueParsers.Bool, scope, path.Field("TextShadow"), v => style.TextShadow = v);
            a.Apply(def.ClickSound, ValueParsers.Text, scope, path.Field("ClickSound"), v => style.ClickSound = v);
            a.Apply(def.HoverSound, ValueParsers.Text, scope, path.Field("HoverSound"), v => style.HoverSound = v);
            return style;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Visibility (Visible combined with the open-time Condition)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>An element's visibility: its <c>Visible</c> value (possibly live) and its <c>Condition</c>, both required.</summary>
        private sealed class Visibility
        {
            private readonly UIElement element;
            private bool requested;
            private bool condition = true;

            internal Visibility(UIElement element)
            {
                this.element = element;
                requested = element.Visible;
            }

            internal void SetRequested(bool value)
            {
                requested = value;
                element.Visible = requested && condition;
            }

            internal void SetCondition(bool value)
            {
                condition = value;
                element.Visible = requested && condition;
            }
        }

        /// <summary>Hides an element while its game state query fails; evaluated each time the menu opens.</summary>
        private sealed class ConditionRefresher : IDataRefresher
        {
            private readonly Visibility visibility;
            private readonly string condition;

            internal ConditionRefresher(Visibility visibility, string condition)
            {
                this.visibility = visibility;
                this.condition = condition;
            }

            public void Refresh(bool opening)
            {
                if (opening)
                {
                    visibility.SetCondition(DataActionRunner.CheckCondition(condition));
                }
            }
        }
    }
}
