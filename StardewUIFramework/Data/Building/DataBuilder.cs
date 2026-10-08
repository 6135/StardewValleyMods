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
            applier.ApplyOr(def.ShowCloseButton, ValueParsers.Bool, DataDefaults.Menu.ShowCloseButton, scope, path.Field("ShowCloseButton"), v => m.ShowCloseButton = v);
            applier.ApplyOr(def.Modal, ValueParsers.Bool, DataDefaults.Menu.Modal, scope, path.Field("Modal"), v => m.Modal = v);
            applier.ApplyOr(def.DimBackground, ValueParsers.Bool, DataDefaults.Menu.DimBackground, scope, path.Field("DimBackground"), v => m.DimBackground = v);
            applier.ApplyOr(def.Anchor, ValueParsers.Anchor, DataDefaults.Menu.Anchor, scope, path.Field("Anchor"), v => m.Anchor = v);
            applier.ApplyOr(def.X, ValueParsers.Int, DataDefaults.Menu.X, scope, path.Field("X"), v => m.X = v);
            applier.ApplyOr(def.Y, ValueParsers.Int, DataDefaults.Menu.Y, scope, path.Field("Y"), v => m.Y = v);
            applier.ApplyOr(def.DrawBox, ValueParsers.Bool, DataDefaults.Menu.DrawBox, scope, path.Field("DrawBox"), v => m.DrawBox = v);
            applier.ApplyOr(def.Padding, ValueParsers.Int, DataDefaults.Menu.Padding, scope, path.Field("Padding"), v => m.Padding = v);
            applier.ApplyOr(def.CloseOnEscape, ValueParsers.Bool, DataDefaults.Menu.CloseOnEscape, scope, path.Field("CloseOnEscape"), v => m.CloseOnEscape = v);
            applier.ApplyOr(def.PlayerLayout, ValueParsers.Bool, DataDefaults.Menu.PlayerLayout, scope, path.Field("PlayerLayout"), v => m.PlayerLayout = v);
            applier.ApplyOr(def.Resizable, ValueParsers.Bool, DataDefaults.Menu.Resizable, scope, path.Field("Resizable"), v => m.Resizable = v);

            // root stack
            applier.ApplyOr(def.Horizontal, ValueParsers.Bool, DataDefaults.Menu.Horizontal, scope, path.Field("Horizontal"), v => menu.Root.Horizontal = v);
            applier.ApplyOr(def.Spacing, ValueParsers.Int, DataDefaults.Menu.Spacing, scope, path.Field("Spacing"), v => menu.Root.Spacing = v);
            applier.ApplyOr(def.Alignment, ValueParsers.Align, DataDefaults.Menu.Alignment, scope, path.Field("Alignment"), v => menu.Root.Alignment = v);

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
