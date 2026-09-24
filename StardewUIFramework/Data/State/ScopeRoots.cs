using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Expressions;
using UIFramework.Hosting;
using UIFramework.Rendering;

namespace UIFramework.Data.State
{
    /// <summary>
    /// Resolves the roots of data expressions for a <see cref="DataScope"/>:
    /// <list type="bullet">
    ///   <item>state: <c>menu.x</c> (Computed values first), <c>session.x</c>, <c>player.x</c>, <c>stat.x</c>, <c>config.x</c> and the qualified <c>menu[owner/menu].x</c>, <c>session[owner].x</c>, <c>player[owner].x</c>, <c>config[owner].x</c>;</item>
    ///   <item><c>event.*</c>: <c>name elementId x y button old new value index oldIndex newIndex oldNumber newNumber key shift ctrl alt link delta error elapsed</c>;</item>
    ///   <item><c>self.*</c> and <c>el[id].*</c>: a composite's exposed values (v1.7), then <c>id visible enabled hovered focused x y width height value text selectedIndex selectedValue selectedRow selectedRows isOpen scroll maxScroll error tag</c>, forms' <c>dirty canUndo canRedo</c>, lists' and grids' <c>rowCount firstVisible sortColumn sortDescending</c> (volatile);</item>
    ///   <item><c>game.*</c>: <c>money totalMoneyEarned season seasonIndex day dayOfWeek year time worldReady playerName farmName location weather screen isMainPlayer</c> (volatile);</item>
    ///   <item><c>ui.*</c>: <c>theme version reducedMotion textScale</c>;</item>
    ///   <item><c>ctx.*</c>: values the menu's owner exposed from C# (<c>Expose*</c>);</item>
    ///   <item><c>model.name.path</c> / <c>model[owner/name].path</c> and <c>@owner/name</c> (v1.6): models, signals, computeds and rows exposed from C# (<see cref="HookRegistry"/>); members of other opaque objects are read by reflection (<see cref="Bridge.ModelAccessor"/>);</item>
    ///   <item><c>row.*</c>, the collection's <c>As</c> name and <c>index</c> (v1.5 collections) and <c>args.*</c> (templates, v1.7): local variables;</item>
    ///   <item>members of item values (<c>row.item.displayName</c>): <c>id qualifiedId name displayName description price category quality stack type</c>.</item>
    /// </list>
    /// </summary>
    internal static class ScopeRoots
    {
        /// <summary>Finds the runtime of a data UI by its <c>menu.*</c> container (set by <see cref="DataService"/>).</summary>
        internal static Func<string, DataRuntime?>? RuntimeResolver { get; set; }

        /// <summary>The exposures of a menu (set by <see cref="DataService"/>).</summary>
        internal static Func<UIMenu, ScreenExposures?>? Exposures { get; set; }

        internal static bool TryResolve(DataScope scope, IReadOnlyList<PathSegment> path, out DataValue value, out bool isVolatile)
        {
            value = DataValue.Null;
            isVolatile = false;
            if (path.Count == 0)
            {
                return false;
            }

            string root = path[0].Key;

            // locals shadow everything (Repeat "As" names, row, args)
            if (scope.Locals != null && scope.Locals.TryGetValue(root, out DataValue local))
            {
                return Walk(scope, local, path, 1, ref value, ref isVolatile);
            }

            if (StateAddress.TryGetScope(root, out StateScope stateScope))
            {
                return TryResolveState(scope, stateScope, path, out value, out isVolatile);
            }

            DataValue start;
            switch (root)
            {
                case "event":
                    return path.Count > 1 && TryEvent(scope, path[1].Key, out start) && Walk(scope, start, path, 2, ref value, ref isVolatile);

                case "self":
                    isVolatile = true;
                    return scope.Element != null && Walk(scope, DataValue.Opaque(new ElementRef(scope.Element, scope.Runtime)), path, 1, ref value, ref isVolatile);

                case "el":
                {
                    isVolatile = true;
                    if (path.Count < 2)
                    {
                        return false;
                    }

                    UIElement? element = FindElement(scope, path[1].Key);
                    return element != null && Walk(scope, DataValue.Opaque(new ElementRef(element, scope.Runtime)), path, 2, ref value, ref isVolatile);
                }

                case "game":
                    isVolatile = true;
                    return path.Count > 1 && TryGame(path[1].Key, out start) && Walk(scope, start, path, 2, ref value, ref isVolatile);

                case "ui":
                    isVolatile = true;
                    return path.Count > 1 && TryUi(path[1].Key, out start) && Walk(scope, start, path, 2, ref value, ref isVolatile);

                case "ctx":
                {
                    isVolatile = true;
                    ScreenExposures? exposures = scope.Menu != null ? Exposures?.Invoke(scope.Menu) : null;
                    if (exposures == null || path.Count < 2)
                    {
                        return false;
                    }

                    string key = path[1].Key;
                    if (!exposures.HasValue(key))
                    {
                        return false;
                    }

                    start = exposures.GetString(key) is { } text ? StateAddress.Infer(text) : DataValue.Null;
                    return Walk(scope, start, path, 2, ref value, ref isVolatile);
                }

                case "model":
                {
                    // model.<name>.<path> (the scope owner's) or model[owner/name].<path>: objects exposed from C# (v1.6)
                    if (path.Count < 2 || Core.UIServices.Hooks is not { } hooks)
                    {
                        return false;
                    }

                    object? model = hooks.ModelOf(HookRegistry.Qualify(path[1].Key, scope.Owner));
                    if (model == null)
                    {
                        return false;
                    }

                    isVolatile = !Bridge.ModelAccessor.Watch(model);
                    return Walk(scope, Bridge.ModelAccessor.ToValue(model), path, 2, ref value, ref isVolatile);
                }

                default:
                    // @owner/name (v1.6): a signal, computed, model or rows exposed from C#; args.* without locals (v1.7)
                    if (root.StartsWith('@') && Core.UIServices.Hooks is { } exposed
                        && exposed.TryReadValue(HookRegistry.Qualify(root, scope.Owner), out start, out bool exposedVolatile))
                    {
                        isVolatile = exposedVolatile;
                        return Walk(scope, start, path, 1, ref value, ref isVolatile);
                    }

                    return false;
            }
        }

        internal static bool TryGetMember(DataScope scope, DataValue target, PathSegment member, out DataValue value, out bool isVolatile)
        {
            isVolatile = false;
            if (target.AsObject() is ElementRef element)
            {
                isVolatile = true;
                return TryElement(element, member.Key, out value);
            }

            if (target.AsObject() is Item item)
            {
                isVolatile = true; // stacks and qualities can change under us
                return Building.RowScope.TryItemMember(item, member.Key, out value);
            }

            // rows of a C# source (DefineDataSource), then plain C# objects exposed to data (models, ExposeRows rows)
            switch (target.AsObject())
            {
                case Bridge.DataSourceHandle.SourceRow row:
                    return row.Source.TryGetField(row.Index, member.Key, out value);
                case Building.TemplateArgs args:
                    return args.TryGet(member.Key, out value, out isVolatile);
                case Building.RowFields:
                    break;
                case { } model:
                    return Bridge.ModelAccessor.TryGetMember(model, member.Key, out value, out isVolatile);
            }

            value = DataValue.Null;
            return false;
        }

        /// <summary>The element <paramref name="id"/> of the scope's menu (or HUD), or null.</summary>
        internal static UIElement? FindElement(DataScope scope, string id)
        {
            return scope.Menu?.Root.FindById(id);
        }

        /// <summary>Walk the rest of <paramref name="path"/> (from <paramref name="index"/>) over <paramref name="start"/>.</summary>
        private static bool Walk(DataScope scope, DataValue start, IReadOnlyList<PathSegment> path, int index, ref DataValue value, ref bool isVolatile)
        {
            DataValue current = start;
            for (int i = index; i < path.Count; i++)
            {
                if (current.TryGetMember(path[i].Key, out DataValue next))
                {
                    current = next;
                    continue;
                }

                if (current.Kind == DataKind.Object && TryGetMember(scope, current, path[i], out next, out bool memberVolatile))
                {
                    current = next;
                    isVolatile |= memberVolatile;
                    continue;
                }

                value = DataValue.Null;
                return false;
            }

            value = current;
            return true;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  State
        // ---------------------------------------------------------------------------------------------------------

        private static bool TryResolveState(DataScope scope, StateScope stateScope, IReadOnlyList<PathSegment> path, out DataValue value, out bool isVolatile)
        {
            value = DataValue.Null;
            isVolatile = false;
            DataStateStore? store = DataStateStore.Active;
            if (store == null)
            {
                return false;
            }

            int nameStart = path.Count > 1 && path[1].IsIndex ? 2 : 1;
            if (nameStart >= path.Count)
            {
                return false;
            }

            string container;
            if (nameStart == 2)
            {
                container = path[1].Key.Trim();
            }
            else
            {
                string? implicitContainer = stateScope switch
                {
                    StateScope.Stat => string.Empty,
                    StateScope.Menu => scope.StateKey,
                    _ => scope.Owner
                };
                if (implicitContainer == null)
                {
                    return false;
                }

                container = implicitContainer;
            }

            if (stateScope == StateScope.Stat)
            {
                container = string.Empty;
            }

            // computed values of the menu (menu.total, menu[owner/menu].total)
            if (stateScope == StateScope.Menu)
            {
                DataRuntime? runtime = nameStart == 1 && scope.Runtime != null ? scope.Runtime : RuntimeResolver?.Invoke(container);
                string first = path[nameStart].Key;
                if (runtime != null && runtime.HasComputed(first))
                {
                    runtime.TryComputed(first, out DataValue computed, out isVolatile);
                    return Walk(scope, computed, path, nameStart + 1, ref value, ref isVolatile);
                }
            }

            // longest dotted name that is a stored value, then member access on it (menu.settings.day → cell "settings.day", else cell "settings" + .day)
            for (int end = path.Count; end > nameStart; end--)
            {
                string name = StateAddress.JoinName(Slice(path, nameStart, end), 0);
                var address = new StateAddress(stateScope, container, name);
                if (store.TryRead(address, out DataValue stored, out bool storedVolatile))
                {
                    isVolatile |= storedVolatile;
                    return Walk(scope, stored, path, end, ref value, ref isVolatile);
                }

                isVolatile |= storedVolatile;
            }

            return false;
        }

        private static IReadOnlyList<PathSegment> Slice(IReadOnlyList<PathSegment> path, int start, int end)
        {
            var result = new PathSegment[end - start];
            for (int i = start; i < end; i++)
            {
                result[i - start] = path[i];
            }

            return result;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  event.*
        // ---------------------------------------------------------------------------------------------------------

        private static bool TryEvent(DataScope scope, string name, out DataValue value)
        {
            if (scope.EventFields != null && scope.EventFields.TryGetValue(name, out value))
            {
                return true;
            }

            value = DataValue.Null;
            switch (name.ToLowerInvariant())
            {
                case "name":
                    value = DataValue.FromString(scope.EventName);
                    return scope.EventName != null;
                case "elementid":
                    value = DataValue.FromString((scope.EventArgs as IUIEvent)?.ElementId ?? scope.Element?.Id);
                    return true;
            }

            switch (scope.EventArgs)
            {
                case IUIClickEvent click:
                    value = name.ToLowerInvariant() switch
                    {
                        "x" => DataValue.FromNumber(click.X),
                        "y" => DataValue.FromNumber(click.Y),
                        "button" => DataValue.FromString(click.Button.ToString()),
                        _ => DataValue.Null
                    };
                    return !value.IsNull;

                case IUIValueEvent changed:
                    value = name.ToLowerInvariant() switch
                    {
                        "old" or "oldvalue" => TypedOld(changed),
                        "new" or "newvalue" or "value" => TypedNew(changed),
                        "oldnumber" => DataValue.FromNumber(changed.OldNumber),
                        "newnumber" => DataValue.FromNumber(changed.NewNumber),
                        "oldbool" => DataValue.FromBool(changed.OldBool),
                        "newbool" => DataValue.FromBool(changed.NewBool),
                        "index" or "newindex" => DataValue.FromNumber(changed.NewIndex),
                        "oldindex" => DataValue.FromNumber(changed.OldIndex),
                        _ => DataValue.Null
                    };
                    return !value.IsNull;

                case IUIKeyEvent key:
                    value = name.ToLowerInvariant() switch
                    {
                        "key" => DataValue.FromString(key.Key.ToString()),
                        "shift" => DataValue.FromBool(key.Shift),
                        "ctrl" => DataValue.FromBool(key.Ctrl),
                        "alt" => DataValue.FromBool(key.Alt),
                        _ => DataValue.Null
                    };
                    return !value.IsNull;

                case string link:
                    value = name.ToLowerInvariant() is "link" or "value" ? DataValue.FromString(link) : DataValue.Null;
                    return !value.IsNull;

                case int delta:
                    value = name.ToLowerInvariant() is "delta" or "value" ? DataValue.FromNumber(delta) : DataValue.Null;
                    return !value.IsNull;

                case double elapsed:
                    value = name.ToLowerInvariant() is "elapsed" or "value" ? DataValue.FromNumber(elapsed) : DataValue.Null;
                    return !value.IsNull;
            }

            return false;
        }

        private static DataValue TypedNew(IUIValueEvent e)
        {
            return e.Element switch
            {
                IUICheckbox => DataValue.FromBool(e.NewBool),
                IUINumberInput or IUISlider => DataValue.FromNumber(e.NewNumber),
                _ => DataValue.FromString(e.NewValue)
            };
        }

        private static DataValue TypedOld(IUIValueEvent e)
        {
            return e.Element switch
            {
                IUICheckbox => DataValue.FromBool(e.OldBool),
                IUINumberInput or IUISlider => DataValue.FromNumber(e.OldNumber),
                _ => DataValue.FromString(e.OldValue)
            };
        }

        // ---------------------------------------------------------------------------------------------------------
        //  self.* / el[id].*
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>An element handed to expressions as an opaque value (members read through <see cref="TryElement"/>).</summary>
        internal sealed class ElementRef
        {
            internal ElementRef(UIElement element, DataRuntime? runtime)
            {
                Element = element;
                Runtime = runtime;
            }

            internal UIElement Element { get; }
            internal DataRuntime? Runtime { get; }

            public override string ToString() => Element.Id;

            public override bool Equals(object? obj) => obj is ElementRef other && other.Element == Element;

            public override int GetHashCode() => Element.GetHashCode();
        }

        private static bool TryElement(ElementRef reference, string member, out DataValue value)
        {
            UIElement e = reference.Element;
            IUIElement pub = e;

            // a composite's exposed values (v1.7: el[id].value) win over the element members of the same name
            if (e is Composite composite && composite.TryReadExposed(member, out object? exposed))
            {
                value = exposed switch
                {
                    string text => StateAddress.Infer(text),
                    double number => DataValue.FromNumber(number),
                    bool flag => DataValue.FromBool(flag),
                    _ => DataValue.Null
                };
                return true;
            }

            Rectangle bounds = pub.Bounds;
            value = member.ToLowerInvariant() switch
            {
                "id" => DataValue.FromString(e.Id),
                "visible" => DataValue.FromBool(e.Visible),
                "enabled" => DataValue.FromBool(e.Enabled),
                "hovered" or "ishovered" => DataValue.FromBool(e.IsHovered),
                "focused" or "isfocused" => DataValue.FromBool(e.IsFocused),
                "x" => DataValue.FromNumber(bounds.X),
                "y" => DataValue.FromNumber(bounds.Y),
                "width" => DataValue.FromNumber(bounds.Width),
                "height" => DataValue.FromNumber(bounds.Height),
                "value" => ValueOf(e),
                "text" => TextOf(e),
                "selectedindex" or "selectedrow" => e switch
                {
                    IUIDropdown d => DataValue.FromNumber(d.SelectedIndex),
                    IUIList l => DataValue.FromNumber(l.SelectedIndex),
                    IUIDataGrid g => DataValue.FromNumber(g.SelectedRow),
                    _ => DataValue.Null
                },
                "selectedrows" => e is IUIDataGrid rows ? DataValue.FromList(Array.ConvertAll(rows.SelectedRows, r => DataValue.FromNumber(r))) : DataValue.Null,
                "rowcount" or "itemcount" => e switch
                {
                    IUIList l => DataValue.FromNumber(l.ItemCount),
                    IUIDataGrid g => DataValue.FromNumber(g.RowCount),
                    _ => DataValue.Null
                },
                "firstvisible" or "firstvisibleindex" => e switch
                {
                    IUIList l => DataValue.FromNumber(l.FirstVisibleIndex),
                    IUIDataGrid g => DataValue.FromNumber(g.FirstVisibleIndex),
                    _ => DataValue.Null
                },
                "sortcolumn" => e is IUIDataGrid sorted ? DataValue.FromString(sorted.SortColumn) : DataValue.Null,
                "sortdescending" => e is IUIDataGrid descending ? DataValue.FromBool(descending.SortDescending) : DataValue.Null,
                "dirty" or "isdirty" => e is IUIForm dirty ? DataValue.FromBool(dirty.IsDirty) : DataValue.Null,
                "canundo" => e is IUIForm undo ? DataValue.FromBool(undo.CanUndo) : DataValue.Null,
                "canredo" => e is IUIForm redo ? DataValue.FromBool(redo.CanRedo) : DataValue.Null,
                "selectedvalue" => e is IUIDropdown dropdown ? DataValue.FromString(dropdown.SelectedValue) : DataValue.Null,
                "isopen" => e is IUIDropdown open ? DataValue.FromBool(open.IsOpen) : DataValue.False,
                "scroll" or "scrolloffset" => e is IUIScrollView sv ? DataValue.FromNumber(sv.ScrollOffset) : DataValue.Null,
                "maxscroll" => e is IUIScrollView max ? DataValue.FromNumber(max.MaxScroll) : DataValue.Null,
                "error" => DataValue.FromString(reference.Runtime?.ErrorOf(e.Id)),
                "valid" => DataValue.FromBool(reference.Runtime?.ErrorOf(e.Id) == null),
                "tag" => DataValue.FromObject(pub.Tag),
                "childcount" => e is UIContainer container ? DataValue.FromNumber(container.ChildCount) : DataValue.Zero,
                _ => DataValue.Null
            };
            return !value.IsNull || member.Equals("error", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>The current value of an input element (bool, number or text), or null for other elements.</summary>
        internal static DataValue ValueOf(UIElement e)
        {
            return e switch
            {
                IUICheckbox c => DataValue.FromBool(c.Value),
                IUITextInput t => DataValue.FromString(t.Value),
                IUINumberInput n => DataValue.FromNumber(n.Value),
                IUISlider s => DataValue.FromNumber(s.Value),
                IUIDropdown d => DataValue.FromString(d.SelectedValue),
                _ => DataValue.Null
            };
        }

        private static DataValue TextOf(UIElement e)
        {
            Func<string>? text = e switch
            {
                Label label => label.TextFunc,
                Button button => button.TextFunc,
                _ => null
            };
            return text == null ? DataValue.Null : DataValue.FromString(e.Consumer.Invoke(e.Id, "Text", text, string.Empty));
        }

        // ---------------------------------------------------------------------------------------------------------
        //  game.* / ui.*
        // ---------------------------------------------------------------------------------------------------------

        private static bool TryGame(string name, out DataValue value)
        {
            bool ready = Context.IsWorldReady;
            Farmer? player = ready ? Game1.player : null;
            value = name.ToLowerInvariant() switch
            {
                "worldready" => DataValue.FromBool(ready),
                "screen" => DataValue.FromNumber(Context.ScreenId),
                "ismainplayer" => DataValue.FromBool(Context.IsMainPlayer),
                "money" => player != null ? DataValue.FromNumber(player.Money) : DataValue.Zero,
                "totalmoneyearned" => player != null ? DataValue.FromNumber(player.totalMoneyEarned) : DataValue.Zero,
                "season" => ready ? DataValue.FromString(Game1.currentSeason) : DataValue.EmptyString,
                "seasonindex" => ready ? DataValue.FromNumber(Game1.seasonIndex) : DataValue.Zero,
                "day" => ready ? DataValue.FromNumber(Game1.dayOfMonth) : DataValue.Zero,
                "dayofweek" => ready ? DataValue.FromString(Game1.shortDayNameFromDayOfSeason(Game1.dayOfMonth)) : DataValue.EmptyString,
                "year" => ready ? DataValue.FromNumber(Game1.year) : DataValue.Zero,
                "time" => ready ? DataValue.FromNumber(Game1.timeOfDay) : DataValue.Zero,
                "playername" => player != null ? DataValue.FromString(player.Name) : DataValue.EmptyString,
                "farmname" => player != null ? DataValue.FromString(player.farmName.Value) : DataValue.EmptyString,
                "location" => ready ? DataValue.FromString(Game1.currentLocation?.NameOrUniqueName ?? string.Empty) : DataValue.EmptyString,
                "weather" => ready ? DataValue.FromString(Game1.currentLocation?.GetWeather()?.Weather ?? string.Empty) : DataValue.EmptyString,
                _ => DataValue.Null
            };
            return !value.IsNull;
        }

        private static bool TryUi(string name, out DataValue value)
        {
            value = name.ToLowerInvariant() switch
            {
                "theme" => DataValue.FromString(Theme.ActiveName),
                "version" => DataValue.FromString(StardewUIApi.Version),
                "reducedmotion" => DataValue.FromBool(Theme.ReducedMotion),
                "textscale" => DataValue.FromNumber(UIServices.Config.TextScale),
                _ => DataValue.Null
            };
            return !value.IsNull;
        }
    }
}
