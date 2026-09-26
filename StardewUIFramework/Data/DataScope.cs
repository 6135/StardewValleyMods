using System;
using System.Collections.Generic;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Data.State;

namespace UIFramework.Data
{
    /// <summary>
    /// Where a data value is read or an action runs: the owner mod, the UI (menu or HUD) and its runtime, the element,
    /// the event being handled, the <c>With</c> narrowing and local variables. Handed to value sources and to every
    /// trigger action run by the data layer (through <c>TriggerActionContext.CustomFields["6135.UIFramework/Scope"]</c>
    /// and the ambient scope of <see cref="Actions.DataActionRunner"/>). Immutable: each narrowing returns a new scope
    /// pointing at its parent.
    /// <para>
    /// It is also the expression scope (<see cref="IExpressionScope"/>) of everything evaluated in it; the roots are
    /// resolved by <see cref="ScopeRoots"/>: <c>menu session player stat config</c> (state), <c>ctx args row event self
    /// el game ui</c> (read-only), <c>.name</c> (relative to <c>With</c>) and locals.
    /// </para>
    /// </summary>
    internal sealed class DataScope : IExpressionScope
    {
        private DataScope(string owner, string menuId, UIMenu? menu, DataRuntime? runtime, UIElement? element, string? eventName, object? eventArgs,
            IReadOnlyDictionary<string, DataValue>? eventFields, PathSegment[]? withPath, IReadOnlyDictionary<string, DataValue>? locals, DataScope? parent)
        {
            Owner = owner;
            MenuId = menuId;
            Menu = menu;
            Runtime = runtime;
            Element = element;
            EventName = eventName;
            EventArgs = eventArgs;
            EventFields = eventFields;
            WithPath = withPath;
            Locals = locals;
            Parent = parent;
        }

        /// <summary>Resolves the consumer context of an owner (set by <see cref="DataService"/>).</summary>
        internal static Func<string, ConsumerContext>? ContextResolver { get; set; }

        /// <summary>Owner mod / content pack id.</summary>
        internal string Owner { get; }

        /// <summary>Menu (or HUD) id without the owner; empty for owner-level scopes (owner hotkeys).</summary>
        internal string MenuId { get; }

        /// <summary>The menu model (for HUDs, the widget's inner menu), when the scope belongs to one.</summary>
        internal UIMenu? Menu { get; }

        /// <summary>The data runtime of the UI (null for owner-level scopes and C# menus).</summary>
        internal DataRuntime? Runtime { get; }

        /// <summary>The element the value / event belongs to (null at menu level).</summary>
        internal UIElement? Element { get; }

        /// <summary>The event being handled (OnClick, OnValueChanged...), or null.</summary>
        internal string? EventName { get; }

        /// <summary>The event's arguments (an <c>IUIEvent</c>, the link text, the scroll delta...), or null.</summary>
        internal object? EventArgs { get; }

        /// <summary>Extra <c>event.*</c> fields (validation value / error, watch old / new, elapsed time...).</summary>
        internal IReadOnlyDictionary<string, DataValue>? EventFields { get; }

        /// <summary>The absolute path <c>.name</c> is relative to (the nearest <c>With</c>), or null.</summary>
        internal PathSegment[]? WithPath { get; }

        /// <summary>Local variables (Phase 3 <c>row</c> / <c>As</c> names, Phase 5 <c>args</c>) visible to this scope.</summary>
        internal IReadOnlyDictionary<string, DataValue>? Locals { get; }

        /// <summary>The scope this one narrows.</summary>
        internal DataScope? Parent { get; }

        /// <summary>The <c>owner/menu</c> key.</summary>
        internal string MenuKey => Owner + "/" + MenuId;

        /// <summary>The container of <c>menu.*</c> in this scope (null for owner-level scopes).</summary>
        internal string? StateKey => Runtime?.StateKey ?? (MenuId.Length > 0 ? MenuKey : null);

        /// <summary>Id used for the per-element callback guard: the element's id, else the menu's.</summary>
        internal string GuardId => Element?.Id ?? (MenuId.Length > 0 ? MenuId : Owner);

        /// <summary>The owner's consumer context (callback guard, signals).</summary>
        internal ConsumerContext Consumer => Menu?.Consumer ?? ContextResolver?.Invoke(Owner) ?? ConsumerContext.None;

        // ---------------------------------------------------------------------------------------------------------
        //  Construction
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>A menu-level scope.</summary>
        internal static DataScope ForMenu(string owner, string menuId, UIMenu? menu) => new(owner, menuId, menu, null, null, null, null, null, null, null, null);

        /// <summary>The scope of a data UI (menu or HUD) built from <paramref name="runtime"/>.</summary>
        internal static DataScope ForRuntime(DataRuntime runtime, UIMenu? menu) => new(runtime.Owner, runtime.Id, menu, runtime, null, null, null, null, null, null, null);

        /// <summary>An owner-level scope (owner hotkeys, actions run outside any UI on behalf of an owner).</summary>
        internal static DataScope ForOwner(string owner) => new(owner, string.Empty, null, null, null, null, null, null, null, null, null);

        /// <summary>This scope narrowed to an element.</summary>
        internal DataScope WithElement(UIElement element) => new(Owner, MenuId, Menu, Runtime, element, null, null, null, WithPath, Locals, this);

        /// <summary>This scope while an event is handled.</summary>
        internal DataScope WithEvent(string eventName, object? args, IReadOnlyDictionary<string, DataValue>? fields = null)
        {
            return new DataScope(Owner, MenuId, Menu, Runtime, Element, eventName, args, fields, WithPath, Locals, this);
        }

        /// <summary>This scope with <c>.name</c> relative to <paramref name="path"/> (already absolute).</summary>
        internal DataScope WithRelative(PathSegment[] path) => new(Owner, MenuId, Menu, Runtime, Element, EventName, EventArgs, EventFields, path, Locals, this);

        /// <summary>This scope with extra local variables (layered over the existing ones).</summary>
        internal DataScope WithLocals(IReadOnlyDictionary<string, DataValue> locals)
        {
            Dictionary<string, DataValue> merged = new(StringComparer.Ordinal);
            if (Locals != null)
            {
                foreach ((string key, DataValue value) in Locals)
                {
                    merged[key] = value;
                }
            }

            foreach ((string key, DataValue value) in locals)
            {
                merged[key] = value;
            }

            return new DataScope(Owner, MenuId, Menu, Runtime, Element, EventName, EventArgs, EventFields, WithPath, merged, this);
        }

        /// <summary>
        /// The absolute form of a <c>With</c> value (<c>menu.settings</c>, or <c>.sub</c> relative to the current one);
        /// null (with an error) when it is not a plain path.
        /// </summary>
        internal PathSegment[]? ResolveWith(string with, out string error)
        {
            error = string.Empty;
            CompiledExpression expression = CompiledExpression.Compile(with.Trim());
            if (expression.Root is not PathNode { StaticPath: { } path })
            {
                error = expression.Error != null ? expression.Error.ToString() : "With must be a plain path such as menu.settings.";
                return null;
            }

            PathSegment[] absolute = ExpandRelative(path);
            if (absolute.Length > 0 && absolute[0].Key == Parser.RelativeRoot)
            {
                error = "'.name' needs an enclosing With.";
                return null;
            }

            return absolute;
        }

        /// <summary>Replace a leading <c>.</c> root with <see cref="WithPath"/> (unchanged when there is none).</summary>
        internal PathSegment[] ExpandRelative(PathSegment[] path)
        {
            if (path.Length == 0 || path[0].Key != Parser.RelativeRoot || WithPath == null)
            {
                return path;
            }

            var result = new PathSegment[WithPath.Length + path.Length - 1];
            Array.Copy(WithPath, result, WithPath.Length);
            Array.Copy(path, 1, result, WithPath.Length, path.Length - 1);
            return result;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  IExpressionScope
        // ---------------------------------------------------------------------------------------------------------

        public bool TryResolve(IReadOnlyList<PathSegment> path, out DataValue value, out bool isVolatile)
        {
            if (path.Count > 0 && path[0].Key == Parser.RelativeRoot)
            {
                if (WithPath == null)
                {
                    value = DataValue.Null;
                    isVolatile = false;
                    return false;
                }

                var absolute = new PathSegment[path.Count];
                for (int i = 0; i < path.Count; i++)
                {
                    absolute[i] = path[i];
                }

                return ScopeRoots.TryResolve(this, ExpandRelative(absolute), out value, out isVolatile);
            }

            return ScopeRoots.TryResolve(this, path, out value, out isVolatile);
        }

        public bool TryGetMember(DataValue target, PathSegment member, out DataValue value, out bool isVolatile)
        {
            return ScopeRoots.TryGetMember(this, target, member, out value, out isVolatile);
        }

        public bool TryCallExternal(string name, ReadOnlySpan<DataValue> args, out DataValue result, out bool isVolatile)
        {
            // @owner/name(...): functions registered from C# (v1.6); a short name is the scope owner's. C# code can read
            // anything, so results are cached per tick only.
            isVolatile = true;
            if (Core.UIServices.Hooks is { } hooks)
            {
                return hooks.TryCallFunction(Hosting.HookRegistry.Qualify(name, Owner), args, out result);
            }

            result = DataValue.Null;
            return false;
        }

        public override string ToString() => (MenuId.Length > 0 ? MenuKey : Owner) + (Element != null ? "#" + Element.Id : string.Empty) + (EventName != null ? " " + EventName : string.Empty);
    }
}
