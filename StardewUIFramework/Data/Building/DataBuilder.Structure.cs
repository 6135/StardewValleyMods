using System;
using System.Collections.Generic;
using System.Linq;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Data.State;

namespace UIFramework.Data.Building
{
    /// <summary>Structural elements (<c>If</c>, <c>Switch</c> / <c>Case</c>) and output bindings (<c>Out</c>).</summary>
    internal sealed partial class DataBuilder
    {
        // ---------------------------------------------------------------------------------------------------------
        //  If
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// An element with <c>If</c>: built only while the expression is true, removed (with its refreshers) when it
        /// turns false. An invisible zero-size anchor keeps its place among its siblings.
        /// </summary>
        private void BuildIf(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            ValueSource<bool>? condition = ctx.Applier.Source(def.If, ValueParsers.Bool, path.Field("If"));
            if (condition == null)
            {
                return; // reported
            }

            if (!condition.IsDynamic)
            {
                if (condition.Get(scope))
                {
                    BuildElement(ctx, parent, def, scope, path);
                }

                return;
            }

            IUISpacer anchor = ctx.Api.AddSpacer(parent, ctx.IdOf(def) + ".if", 0, 0);
            anchor.Visible = false;
            var refresher = new IfRefresher(this, ctx, (UIContainer)parent, (UIElement)anchor, def, scope, path, condition);
            refresher.Refresh(opening: false);
            ctx.Applier.AddRefresher(refresher);
        }

        private sealed class IfRefresher : IDataRefresher
        {
            private readonly DataBuilder builder;
            private readonly BuildContext ctx;
            private readonly UIContainer parent;
            private readonly UIElement anchor;
            private readonly ElementDefinition def;
            private readonly DataScope scope;
            private readonly DataPath path;
            private readonly ValueSource<bool> condition;
            private readonly RefresherGroup group;
            private UIElement? built;

            internal IfRefresher(DataBuilder builder, BuildContext ctx, UIContainer parent, UIElement anchor, ElementDefinition def, DataScope scope, DataPath path, ValueSource<bool> condition)
            {
                this.builder = builder;
                this.ctx = ctx;
                this.parent = parent;
                this.anchor = anchor;
                this.def = def;
                this.scope = scope;
                this.path = path;
                this.condition = condition;
                group = new RefresherGroup(ctx.Runtime.Owner, ctx.IdOf(def));
            }

            public void Refresh(bool opening)
            {
                bool wanted = condition.Get(scope);
                if (wanted && built == null)
                {
                    group.Clear();
                    try
                    {
                        built = builder.BuildElement(ctx.WithGroup(group), parent, def, scope, path);
                    }
                    catch (Exception ex)
                    {
                        UIServices.Log($"[{ctx.Runtime.Owner}] {path}: could not be built: {ex.Message}", LogLevel.Error);
                        return;
                    }

                    int index = IndexOf(parent, anchor);
                    parent.Insert(index + 1, built);
                    group.Refresh(opening: true);
                    return;
                }

                if (!wanted && built != null)
                {
                    parent.Remove(built);
                    built = null;
                    group.Clear();
                    return;
                }

                if (built != null)
                {
                    group.Refresh(opening);
                }
            }

            private static int IndexOf(UIContainer container, UIElement child)
            {
                IReadOnlyList<UIElement> children = container.Children;
                for (int i = 0; i < children.Count; i++)
                {
                    if (children[i] == child)
                    {
                        return i;
                    }
                }

                return children.Count - 1;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Switch / Case
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A <c>Switch</c>: a zero-spacing stack whose children are pages; the page whose <c>Case</c> matches the
        /// expression is shown, the others are hidden. Pages are built the first time they are shown (tabs cost nothing
        /// until opened) and only the shown page's values are refreshed.
        /// </summary>
        private void BuildSwitch(BuildContext ctx, UIContainer stack, ElementDefinition def, DataScope scope, DataPath path)
        {
            if (string.IsNullOrWhiteSpace(def.Switch))
            {
                ctx.Log.Error(path.Field("Switch"), "a Switch needs an expression selecting the page (e.g. \"menu.tab\").");
                return;
            }

            var pages = new List<SwitchPage>();
            if (def.Children != null)
            {
                for (int i = 0; i < def.Children.Count; i++)
                {
                    ElementDefinition? page = def.Children[i];
                    if (page?.Type == null || page.Id == null)
                    {
                        continue;
                    }

                    pages.Add(new SwitchPage(page, path.Field("Children").Index(i, page.Id), CaseValues(page.Case)));
                }
            }

            var refresher = new SwitchRefresher(this, ctx, stack, def.Switch, scope, pages);
            refresher.Refresh(opening: false);
            ctx.Applier.AddRefresher(refresher);
        }

        /// <summary>The values of a <c>Case</c> (null = the default page).</summary>
        private static string[]? CaseValues(string? raw)
        {
            if (raw == null || raw.Trim() == "*")
            {
                return null;
            }

            return raw.Split(',').Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        }

        private sealed class SwitchPage
        {
            internal SwitchPage(ElementDefinition def, DataPath path, string[]? cases)
            {
                Def = def;
                Path = path;
                Cases = cases;
            }

            internal ElementDefinition Def { get; }
            internal DataPath Path { get; }
            internal string[]? Cases { get; }
            internal UIElement? Element { get; set; }
            internal RefresherGroup? Group { get; set; }
        }

        private sealed class SwitchRefresher : IDataRefresher
        {
            private readonly DataBuilder builder;
            private readonly BuildContext ctx;
            private readonly UIContainer stack;
            private readonly string expression;
            private readonly DataScope scope;
            private readonly List<SwitchPage> pages;
            private SwitchPage? current;
            private bool reported;

            internal SwitchRefresher(DataBuilder builder, BuildContext ctx, UIContainer stack, string expression, DataScope scope, List<SwitchPage> pages)
            {
                this.builder = builder;
                this.ctx = ctx;
                this.stack = stack;
                this.expression = expression;
                this.scope = scope;
                this.pages = pages;
            }

            public void Refresh(bool opening)
            {
                DataValue value = ctx.Applier.Resolver.Evaluate(expression, scope, out string? error);
                if (error != null && !reported)
                {
                    reported = true;
                    UIServices.Log($"[{ctx.Runtime.Owner}] Switch '{stack.Id}': '{expression}': {error}", LogLevel.Warn);
                }

                string key = value.AsString();
                SwitchPage? selected = pages.FirstOrDefault(p => p.Cases != null && p.Cases.Any(c => string.Equals(c, key, StringComparison.OrdinalIgnoreCase)))
                    ?? pages.FirstOrDefault(p => p.Cases == null);

                if (selected != current)
                {
                    if (current?.Element != null)
                    {
                        current.Element.Visible = false;
                    }

                    current = selected;
                    if (selected != null)
                    {
                        if (selected.Element == null)
                        {
                            selected.Group = new RefresherGroup(ctx.Runtime.Owner, selected.Def.Id!);
                            try
                            {
                                selected.Element = builder.BuildElement(ctx.WithGroup(selected.Group), stack, selected.Def, scope, selected.Path);
                            }
                            catch (Exception ex)
                            {
                                UIServices.Log($"[{ctx.Runtime.Owner}] {selected.Path}: could not be built: {ex.Message}", LogLevel.Error);
                            }
                        }

                        if (selected.Element != null)
                        {
                            selected.Element.Visible = true;
                        }

                        selected.Group?.Refresh(opening: true);
                    }

                    stack.InvalidateLayout();
                    return;
                }

                current?.Group?.Refresh(opening);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Out
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Output bindings: runtime values of the element written into state whenever they change.</summary>
        private void AddOutputs(BuildContext ctx, UIElement element, Dictionary<string, string> outputs, DataScope scope, DataPath path)
        {
            var bindings = new List<(string Key, StateAddress Address)>();
            foreach ((string key, string? target) in outputs)
            {
                string? canonical = ElementTypes.OutKeys.FirstOrDefault(k => string.Equals(k, key?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (canonical == null)
                {
                    string? suggestion = DataValidator.Suggest(key ?? string.Empty, ElementTypes.OutKeys);
                    ctx.Log.Warn(path.Field(key ?? "?"), $"unknown output '{key}'{(suggestion != null ? $" (did you mean '{suggestion}'?)" : string.Empty)}; it is ignored.");
                    continue;
                }

                if (!StateAddress.TryParse(target, scope, allowBare: true, out StateAddress address, out string error))
                {
                    ctx.Log.Error(path.Field(canonical), error);
                    continue;
                }

                if (address.Scope == StateScope.Stat)
                {
                    ctx.Log.Warn(path.Field(canonical), "outputs cannot write stat.* values; it is ignored.");
                    continue;
                }

                bindings.Add((canonical, address));
            }

            if (bindings.Count > 0)
            {
                ctx.Applier.AddRefresher(new OutRefresher(element, bindings, store));
            }
        }

        private sealed class OutRefresher : IDataRefresher
        {
            private readonly UIElement element;
            private readonly List<(string Key, StateAddress Address)> bindings;
            private readonly DataStateStore store;

            internal OutRefresher(UIElement element, List<(string Key, StateAddress Address)> bindings, DataStateStore store)
            {
                this.element = element;
                this.bindings = bindings;
                this.store = store;
            }

            public void Refresh(bool opening)
            {
                if (element.OwnerMenu == null)
                {
                    return;
                }

                foreach ((string key, StateAddress address) in bindings)
                {
                    DataValue value = Read(key);
                    if (!store.Read(address).Equals(value))
                    {
                        store.Write(address, value, out _);
                    }
                }
            }

            private DataValue Read(string key)
            {
                UIElement e = element;
                IUIElement pub = e;
                return key switch
                {
                    "IsHovered" => DataValue.FromBool(e.IsHovered),
                    "IsFocused" => DataValue.FromBool(e.IsFocused),
                    "Visible" => DataValue.FromBool(e.Visible),
                    "ScrollOffset" => e switch
                    {
                        IUIScrollView scroll => DataValue.FromNumber(scroll.ScrollOffset),
                        IUIList list => DataValue.FromNumber(list.FirstVisibleIndex),
                        IUIDataGrid grid => DataValue.FromNumber(grid.FirstVisibleIndex),
                        _ => DataValue.Zero
                    },
                    "MaxScroll" => e is IUIScrollView max ? DataValue.FromNumber(max.MaxScroll) : DataValue.Zero,
                    "SelectedIndex" or "SelectedRow" => e switch
                    {
                        IUIDropdown d => DataValue.FromNumber(d.SelectedIndex),
                        IUIList l => DataValue.FromNumber(l.SelectedIndex),
                        IUIDataGrid g => DataValue.FromNumber(g.SelectedRow),
                        _ => DataValue.FromNumber(-1)
                    },
                    "SelectedValue" => e is IUIDropdown dropdown ? DataValue.FromString(dropdown.SelectedValue) : DataValue.EmptyString,
                    "Value" => ScopeRoots.ValueOf(e),
                    "Text" => e is IUILabel label ? DataValue.FromString(e.Consumer.Invoke(e.Id, "Text", label.Text, string.Empty)) : e is IUIButton button ? DataValue.FromString(e.Consumer.Invoke(e.Id, "Text", button.Text, string.Empty)) : DataValue.EmptyString,
                    "IsOpen" => e is IUIDropdown open ? DataValue.FromBool(open.IsOpen) : DataValue.False,
                    "X" => DataValue.FromNumber(pub.Bounds.X),
                    "Y" => DataValue.FromNumber(pub.Bounds.Y),
                    "Width" => DataValue.FromNumber(pub.Bounds.Width),
                    "Height" => DataValue.FromNumber(pub.Bounds.Height),
                    _ => DataValue.Null
                };
            }
        }
    }
}
