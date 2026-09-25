using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
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
    /// Cross-mod data (v1.7): templates (a menu's <c>Templates</c> and the owner's, expanded at build time), data
    /// composites (the <c>Composites</c> asset, built per instance by <see cref="DataComposites"/>), their
    /// <c>Outlet</c> placeholders, <c>args.*</c>, the instance side of composites (<c>On</c>, dynamic names) and a
    /// menu's <c>Expose</c> / <c>Commands</c>.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>How deep template / composite bodies may nest (a template that expands itself stops here).</summary>
        private const int MaxBodyDepth = 16;

        /// <summary>The composite name used while a dynamic <c>Composite</c> name is empty.</summary>
        private const string NoComposite = "(none)";

        // ---------------------------------------------------------------------------------------------------------
        //  Templates
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Remember the UI's own templates (a menu's <c>Templates</c>).</summary>
        internal static void RegisterTemplates(DataRuntime runtime, Dictionary<string, TemplateDefinition>? templates)
        {
            if (templates == null)
            {
                return;
            }

            foreach ((string name, TemplateDefinition? template) in templates)
            {
                if (template != null && !string.IsNullOrWhiteSpace(name))
                {
                    runtime.Templates[name.Trim()] = template;
                }
            }
        }

        /// <summary>The template <paramref name="name"/>: the UI's own first, then its owner's (<c>Owners</c>).</summary>
        private TemplateDefinition? FindTemplate(DataRuntime runtime, string name)
        {
            if (runtime.Templates.TryGetValue(name, out TemplateDefinition? local))
            {
                return local;
            }

            Dictionary<string, TemplateDefinition>? shared = owners(runtime.Owner)?.Templates;
            return shared?.FirstOrDefault(p => string.Equals(p.Key.Trim(), name, StringComparison.OrdinalIgnoreCase)).Value;
        }

        /// <summary>
        /// A template instance: a stack (the instance's id, common members and layout of the template) holding the
        /// template's body, built in the caller's scope with <c>args.*</c> (its arguments, defaults applied). Body ids
        /// are prefixed with the instance id; the instance's children go into the body's Outlets.
        /// </summary>
        private IUIElement CreateTemplate(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            string name = def.Template?.Trim() ?? string.Empty;
            TemplateDefinition? template = name.Length > 0 ? FindTemplate(ctx.Runtime, name) : null;
            PropertyApplier a = ctx.Applier;
            bool horizontal = template != null && a.Initial(template.Horizontal, ValueParsers.Bool, false, scope, path.Field("Horizontal"));
            int spacing = template == null ? 0 : a.Initial(template.Spacing, ValueParsers.Int, 0, scope, path.Field("Spacing"));
            IUIStack host = ctx.Api.AddStack(parent, ctx.IdOf(def), horizontal, spacing);
            if (template == null)
            {
                ctx.Log.Error(path.Field("Template"), $"no template '{name}' in the menu's Templates or the owner's {DataAssets.ShortName(DataAssets.Owners)} entry; the instance is empty.");
                return host;
            }

            if (template.Alignment != null)
            {
                host.Alignment = a.Initial(template.Alignment, ValueParsers.Align, UIAlign.Start, scope, path.Field("Alignment"));
            }

            if (ctx.Depth >= MaxBodyDepth)
            {
                ctx.Log.Error(path, $"templates nest deeper than {MaxBodyDepth} levels (does '{name}' expand itself?); the instance is empty.");
                return host;
            }

            var bag = new CompositeArgs();
            if (def.Args != null)
            {
                foreach ((string key, Newtonsoft.Json.Linq.JToken? token) in def.Args)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        bag.SetData(key.Trim(), new DataArgument(key.Trim(), token, scope));
                    }
                }
            }

            // inside a data composite's body, _Publish keeps raising the composite's events
            Composite? enclosing = TemplateArgs.Of(scope)?.Host;
            var args = new TemplateArgs(name, template.Params, bag, enclosing);
            DataScope bodyScope = scope.WithLocals(new Dictionary<string, DataValue>(StringComparer.Ordinal) { ["args"] = DataValue.Opaque(args) });
            var outlets = new OutletBinding(ctx, scope, def.Children, path.Field("Children"));
            var body = (UIContainer)host;
            BuildChildren(ctx.ForBody(ctx.IdOf(def), outlets), body, template.Children, bodyScope, path.Field("Template:" + name));
            PlaceUnrouted(outlets, template.Children, body, name, path);
            return host;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Outlets
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// The children of a template / composite instance, grouped by the <c>Outlet</c> they name (empty = the default
        /// one), with the context and scope they are built in (the caller's: they read the caller's locals and state).
        /// </summary>
        private sealed class OutletBinding
        {
            private readonly Dictionary<string, List<(ElementDefinition Def, DataPath Path)>> routes = new(StringComparer.OrdinalIgnoreCase);
            private readonly HashSet<string> taken = new(StringComparer.OrdinalIgnoreCase);

            internal OutletBinding(BuildContext ctx, DataScope scope, List<ElementDefinition>? children, DataPath path)
            {
                Ctx = ctx;
                Scope = scope;
                if (children == null)
                {
                    return;
                }

                for (int i = 0; i < children.Count; i++)
                {
                    ElementDefinition? child = children[i];
                    if (child?.Type == null || child.Id == null)
                    {
                        continue;
                    }

                    // a placeholder forwarded as a child (a template instance inside another body) goes to the default outlet
                    string name = child.Type == ElementTypes.Outlet ? string.Empty : Normalize(child.Outlet);
                    if (!routes.TryGetValue(name, out List<(ElementDefinition, DataPath)>? list))
                    {
                        routes[name] = list = new List<(ElementDefinition, DataPath)>();
                    }

                    list.Add((child, path.Index(i, child.Id)));
                }
            }

            internal BuildContext Ctx { get; }
            internal DataScope Scope { get; }

            /// <summary>The outlet name as stored: empty for the default outlet ("", "default").</summary>
            internal static string Normalize(string? name)
            {
                string text = name?.Trim() ?? string.Empty;
                return text.Equals("default", StringComparison.OrdinalIgnoreCase) ? string.Empty : text;
            }

            /// <summary>The children routed to <paramref name="name"/> (an outlet inside an If / Switch page takes them again when it is rebuilt).</summary>
            internal bool TryTake(string name, out List<(ElementDefinition Def, DataPath Path)> children)
            {
                if (routes.TryGetValue(name, out List<(ElementDefinition, DataPath)>? list))
                {
                    taken.Add(name);
                    children = list;
                    return true;
                }

                children = new List<(ElementDefinition, DataPath)>();
                return false;
            }

            /// <summary>The routes no outlet took and the body does not declare (outlets in a page not built yet take theirs later).</summary>
            internal IEnumerable<(string Name, List<(ElementDefinition Def, DataPath Path)> Children)> Unrouted(List<ElementDefinition>? body)
            {
                HashSet<string>? declared = null;
                foreach ((string name, List<(ElementDefinition, DataPath)> list) in routes)
                {
                    if (taken.Contains(name))
                    {
                        continue;
                    }

                    declared ??= DeclaredOutlets(body);
                    if (!declared.Contains(name))
                    {
                        yield return (name, list);
                    }
                }
            }

            /// <summary>Every Outlet name a body declares (also inside If / Switch pages and nested instances' children).</summary>
            private static HashSet<string> DeclaredOutlets(List<ElementDefinition>? body)
            {
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pending = new Stack<ElementDefinition>(body?.Where(d => d != null) ?? Enumerable.Empty<ElementDefinition>());
                while (pending.Count > 0)
                {
                    ElementDefinition def = pending.Pop();
                    if (def.Type == ElementTypes.Outlet)
                    {
                        names.Add(Normalize(def.Outlet));
                    }

                    if (def.Children != null)
                    {
                        foreach (ElementDefinition? child in def.Children)
                        {
                            if (child != null)
                            {
                                pending.Push(child);
                            }
                        }
                    }
                }

                return names;
            }
        }

        /// <summary>An <c>Outlet</c> placeholder: the instance's children routed to it, else its own children (fallback content).</summary>
        private void BuildOutlet(BuildContext ctx, UIContainer placeholder, ElementDefinition def, DataScope scope, DataPath path)
        {
            string name = OutletBinding.Normalize(def.Outlet);
            if (ctx.Outlets == null)
            {
                ctx.Log.Warn(path.Field("Outlet"), "an Outlet only receives children inside a template or data composite body; its own children are shown.");
            }
            else if (ctx.Outlets.TryTake(name, out List<(ElementDefinition Def, DataPath Path)> routed) && routed.Count > 0)
            {
                foreach ((ElementDefinition child, DataPath childPath) in routed)
                {
                    BuildOne(ctx.Outlets.Ctx, placeholder, child, ctx.Outlets.Scope, childPath);
                }

                return;
            }
            else
            {
                // no children for this outlet: its fallback content
            }

            BuildChildren(ctx, placeholder, def.Children, scope, path.Field("Children"));
        }

        /// <summary>Children routed to an outlet the body does not have: shown at the end of the instance (with a warning) rather than lost.</summary>
        private void PlaceUnrouted(OutletBinding outlets, List<ElementDefinition>? body, UIContainer host, string name, DataPath path)
        {
            foreach ((string outlet, List<(ElementDefinition Def, DataPath Path)> children) in outlets.Unrouted(body).ToArray())
            {
                outlets.Ctx.Log.Warn(path.Field("Children"), outlet.Length == 0
                    ? $"'{name}' has no default Outlet; {children.Count} child(ren) without an \"Outlet\" are added at its end."
                    : $"'{name}' has no Outlet '{outlet}'; {children.Count} child(ren) are added at its end.");
                foreach ((ElementDefinition child, DataPath childPath) in children)
                {
                    BuildOne(outlets.Ctx, host, child, outlets.Scope, childPath);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Composite instances (data side of the instance)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// What a data composite element hands to the composite's builder through its argument bag: the instance's
        /// children, the context and scope they are built in, and the refresher group they register into (cleared on
        /// every rebuild). <see cref="Consumed"/> tells the element whether a data body placed them in its Outlets or
        /// they still go into the <c>ContentTarget</c> (C# composites).
        /// </summary>
        private sealed class InstancePayload
        {
            internal InstancePayload(BuildContext ctx, DataScope scope, List<ElementDefinition>? children, DataPath path, RefresherGroup childGroup)
            {
                Ctx = ctx;
                Scope = scope;
                Children = children;
                Path = path;
                ChildGroup = childGroup;
            }

            internal BuildContext Ctx { get; }
            internal DataScope Scope { get; }
            internal List<ElementDefinition>? Children { get; }
            internal DataPath Path { get; }
            internal RefresherGroup ChildGroup { get; }

            /// <summary>Set by a data composite's body build (its Outlets placed the children).</summary>
            internal bool Consumed { get; set; }
        }

        /// <summary>After a composite element was created: its children (C# composites: into the ContentTarget) and its <c>On</c> subscriptions.</summary>
        private void FinishComposite(BuildContext ctx, Composite composite, ElementDefinition def, DataScope baseScope, DataScope elementScope, DataPath path)
        {
            if (composite.ArgsBag.DataPayload is InstancePayload payload && !payload.Consumed)
            {
                PlaceContent(composite, def, payload);
            }

            if (def.On == null)
            {
                return;
            }

            foreach ((string rawEvent, List<ActionDefinition>? actions) in def.On)
            {
                string eventName = rawEvent?.Trim() ?? string.Empty;
                if (eventName.Length == 0 || actions == null || actions.Count == 0)
                {
                    continue;
                }

                DataScope eventScope = elementScope.WithEvent("On:" + eventName, null);
                composite.Subscribe(eventName, () => DataActionRunner.Run(actions, eventScope));
            }
        }

        /// <summary>The children of an instance of a C# composite go into its ContentTarget.</summary>
        private void PlaceContent(Composite composite, ElementDefinition def, InstancePayload payload)
        {
            if (def.Children is not { Count: > 0 })
            {
                return;
            }

            payload.ChildGroup.Clear();
            BuildContext ctx = payload.Ctx.WithGroup(payload.ChildGroup);
            BuildChildren(ctx, ContentTargetOf(composite, def, ctx, payload.Path), def.Children, payload.Scope, payload.Path.Field("Children"));
        }

        /// <summary>A <c>Composite</c> name that is an expression: another composite is instantiated in place whenever it changes.</summary>
        private sealed class CompositeNameRefresher : IDataRefresher
        {
            private readonly DataBuilder builder;
            private readonly BuildContext ctx;
            private readonly Composite composite;
            private readonly ElementDefinition def;
            private readonly DataScope scope;
            private readonly DataPath path;
            private readonly ValueSource<string> name;
            private readonly InstancePayload payload;
            private readonly HashSet<string> reported = new(StringComparer.Ordinal);

            internal CompositeNameRefresher(DataBuilder builder, BuildContext ctx, Composite composite, ElementDefinition def, DataScope scope, DataPath path, ValueSource<string> name, InstancePayload payload)
            {
                this.builder = builder;
                this.ctx = ctx;
                this.composite = composite;
                this.def = def;
                this.scope = scope;
                this.path = path;
                this.name = name;
                this.payload = payload;
            }

            public void Refresh(bool opening)
            {
                string next = name.Get(scope).Trim();
                if (next.Length == 0)
                {
                    next = NoComposite;
                }

                if (next == composite.CompositeName)
                {
                    return;
                }

                if (next != NoComposite && !ctx.Api.HasComposite(next) && reported.Add(next))
                {
                    UIServices.Log($"[{ctx.Runtime.Owner}] {path.Field("Composite")}: no composite '{next}' is defined; the element is empty.", LogLevel.Warn);
                }

                payload.Consumed = false;
                payload.ChildGroup.Clear();
                composite.Retarget(next);
                if (!payload.Consumed)
                {
                    builder.PlaceContent(composite, def, payload);
                }
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Data composites (the body, built per instance)
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Fill <paramref name="host"/> (a composite instance, C# or data) with the body of the data composite of
        /// <paramref name="runtime"/>: built through the composite owner's facade, in the composite's scope with
        /// <c>args.*</c>, ids prefixed with the instance id. A data instance's children go into the body's Outlets;
        /// <c>Expose</c> / <c>Commands</c> become the host's exposed values and commands. The body's live values are
        /// refreshed by the menu the instance is attached to.
        /// </summary>
        internal void BuildCompositeBody(Composite host, CompositeArgs bag, DataCompositeRuntime runtime, StardewUIApi ownerApi)
        {
            DataCompositeDefinition def = runtime.Definition;
            UIMenu? menu = host.OwnerMenu;
            RefresherGroup group = host.DataGroup ??= new RefresherGroup(runtime.Owner, host.Id);
            group.Clear();
            var applier = new PropertyApplier(resolver, group, runtime.Messages);
            var args = new TemplateArgs(runtime.Name, def.Params, bag, host);
            foreach (string missing in args.MissingRequired())
            {
                UIServices.Log($"[{runtime.Owner}] composite '{runtime.Name}' (element '{host.Id}' of {menu?.ToString() ?? "no menu"}): the required argument '{missing}' is not set.", LogLevel.Warn);
            }

            DataScope scope = DataScope.ForRuntime(runtime, menu).WithLocals(new Dictionary<string, DataValue>(StringComparer.Ordinal) { ["args"] = DataValue.Opaque(args) });

            // the instance's children (data instances): built by the caller, into this body's outlets
            InstancePayload? payload = bag.DataPayload as InstancePayload;
            OutletBinding? outlets = null;
            int depth = 0;
            if (payload != null)
            {
                payload.Consumed = true;
                payload.ChildGroup.Clear();
                outlets = new OutletBinding(payload.Ctx.WithGroup(payload.ChildGroup), payload.Scope, payload.Children, payload.Path.Field("Children"));
                depth = payload.Ctx.Depth + 1;
            }

            if (depth >= MaxBodyDepth)
            {
                UIServices.Log($"[{runtime.Owner}] composite '{runtime.Name}' nests deeper than {MaxBodyDepth} levels (does it contain itself?); '{host.Id}' is empty.", LogLevel.Error);
                return;
            }

            var ctx = new BuildContext(ownerApi, applier, runtime, host.Id + ".", outlets ?? new OutletBinding(new BuildContext(ownerApi, applier, runtime), scope, null, runtime.Path), true, depth);
            UIContainer body = host;
            if (def.Horizontal != null || def.Spacing != null || def.Alignment != null)
            {
                IUIStack stack = ownerApi.AddStack(host, host.Id + ".body", applier.Initial(def.Horizontal, ValueParsers.Bool, false, scope, runtime.Path.Field("Horizontal")), applier.Initial(def.Spacing, ValueParsers.Int, 0, scope, runtime.Path.Field("Spacing")));
                if (def.Alignment != null)
                {
                    stack.Alignment = applier.Initial(def.Alignment, ValueParsers.Align, UIAlign.Start, scope, runtime.Path.Field("Alignment"));
                }

                body = (UIContainer)stack;
            }

            BuildChildren(ctx, body, def.Children, scope, runtime.Path.Field("Children"));
            if (outlets != null)
            {
                PlaceUnrouted(outlets, def.Children, body, runtime.Name, payload!.Path);
            }

            ExposeCompositeValues(host, def, scope, runtime);
            if (menu != null)
            {
                menu.ExtensionRefresh[host] = group.Refresh;
                group.Refresh(opening: menu.IsOpen);
            }
        }

        /// <summary>A data composite's <c>Expose</c> (each value as text, number and bool) and <c>Commands</c>.</summary>
        private void ExposeCompositeValues(Composite host, DataCompositeDefinition def, DataScope scope, DataCompositeRuntime runtime)
        {
            if (def.Expose != null)
            {
                foreach ((string rawKey, string? raw) in def.Expose)
                {
                    string key = rawKey?.Trim() ?? string.Empty;
                    if (key.Length == 0 || string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    string expression = raw;
                    host.Expose(key, () => ReadExposed(expression, scope, runtime).AsString());
                    host.ExposeNumber(key, () => ReadExposed(expression, scope, runtime).AsNumber());
                    host.ExposeBool(key, () => ReadExposed(expression, scope, runtime).AsBool());
                }
            }

            if (def.Commands != null)
            {
                foreach ((string rawKey, List<ActionDefinition>? actions) in def.Commands)
                {
                    string key = rawKey?.Trim() ?? string.Empty;
                    if (key.Length == 0 || actions == null || actions.Count == 0)
                    {
                        continue;
                    }

                    DataScope commandScope = scope.WithEvent("Command:" + key, null);
                    host.ExposeCommand(key, () => DataActionRunner.Run(actions, commandScope));
                }
            }
        }

        /// <summary>Evaluate an exposed value (a failure is reported once per composite and value).</summary>
        private DataValue ReadExposed(string expression, DataScope scope, DataRuntime runtime)
        {
            DataValue value = resolver.Evaluate(expression, scope, out string? error);
            if (error != null && runtime.Reported.Add("Expose:" + expression))
            {
                UIServices.Log($"[{runtime.Owner}] {runtime.Path.Field("Expose")}: '{expression}': {error}", LogLevel.Warn);
            }

            return value;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Menu Expose / Commands
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A data menu's <c>Expose</c> and <c>Commands</c>: the screen context contributors read (<c>ctx.*</c> in data,
        /// <see cref="IUIScreenContext"/> in C#). Keys a previous build exposed but this one does not are removed.
        /// </summary>
        private void ApplyExposures(StardewUIApi api, UIMenu menu, DataMenuRuntime runtime, MenuDefinition def, DataScope scope, DataPath path)
        {
            foreach (string key in runtime.ExposedKeys)
            {
                api.Expose(menu, key, null!);
                api.ExposeNumber(menu, key, null!);
                api.ExposeBool(menu, key, null!);
            }

            foreach (string key in runtime.CommandKeys)
            {
                api.ExposeCommand(menu, key, null!);
            }

            runtime.ExposedKeys.Clear();
            runtime.CommandKeys.Clear();

            if (def.Expose != null)
            {
                foreach ((string rawKey, string? raw) in def.Expose)
                {
                    string key = rawKey?.Trim() ?? string.Empty;
                    if (key.Length == 0 || string.IsNullOrWhiteSpace(raw))
                    {
                        continue;
                    }

                    string expression = raw;
                    DataPath valuePath = path.Field("Expose").Field(key);
                    api.Expose(menu, key, () => ReadMenuExposed(expression, scope, runtime, valuePath).AsString());
                    api.ExposeNumber(menu, key, () => ReadMenuExposed(expression, scope, runtime, valuePath).AsNumber());
                    api.ExposeBool(menu, key, () => ReadMenuExposed(expression, scope, runtime, valuePath).AsBool());
                    runtime.ExposedKeys.Add(key);
                }
            }

            if (def.Commands != null)
            {
                foreach ((string rawKey, List<ActionDefinition>? actions) in def.Commands)
                {
                    string key = rawKey?.Trim() ?? string.Empty;
                    if (key.Length == 0 || actions == null || actions.Count == 0)
                    {
                        continue;
                    }

                    DataScope commandScope = scope.WithEvent("Command:" + key, null);
                    api.ExposeCommand(menu, key, () => DataActionRunner.Run(actions, commandScope));
                    runtime.CommandKeys.Add(key);
                }
            }
        }

        private DataValue ReadMenuExposed(string expression, DataScope scope, DataMenuRuntime runtime, DataPath path)
        {
            DataValue value = resolver.Evaluate(expression, scope, out string? error);
            if (error != null && runtime.Reported.Add(path.ToString()))
            {
                UIServices.Log($"[{runtime.Owner}] {path}: '{expression}': {error}", LogLevel.Warn);
            }

            return value;
        }
    }
}
