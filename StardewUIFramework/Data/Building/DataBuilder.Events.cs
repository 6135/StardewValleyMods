using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Actions;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace UIFramework.Data.Building
{
    /// <summary>Events: action lists with <c>event.*</c>, <c>Keys</c>, <c>Validate</c> / <c>OnInvalid</c> and <c>OnUpdate</c>.</summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>Each action list becomes a handler run in the element's scope (inside the owner's callback guard).</summary>
        private void WireEvents(BuildContext ctx, IUIElement e, ElementDefinition def, DataScope scope, DataPath path)
        {
            SetIf(DataActionRunner.Handler<IUIClickEvent>(def.OnClick, scope, "OnClick"), h => e.OnClick = h);
            SetIf(DataActionRunner.Handler<IUIClickEvent>(def.OnRightClick, scope, "OnRightClick"), h => e.OnRightClick = h);
            SetIf(DataActionRunner.Handler<IUIElement>(def.OnHover, scope, "OnHover"), h => e.OnHover = h);
            SetIf(DataActionRunner.Handler<IUIElement>(def.OnHoverEnd, scope, "OnHoverEnd"), h => e.OnHoverEnd = h);
            SetIf(DataActionRunner.Handler<IUIElement>(def.OnFocus, scope, "OnFocus"), h => e.OnFocus = h);
            SetIf(DataActionRunner.Handler<IUIElement>(def.OnBlur, scope, "OnBlur"), h => e.OnBlur = h);
            SetIf(KeyHandler(def.Keys, scope, path.Field("Keys"), ctx.Log), h => e.OnKey = h);

            if (e is IUIList or IUIDataGrid)
            {
                WireCollectionEvents(ctx, e, def, scope);
                return;
            }

            if (e is IUIForm form)
            {
                WireFormEvents(form, def, scope);
                return;
            }

            Action<IUIValueEvent>? changed = DataActionRunner.Handler<IUIValueEvent>(def.OnValueChanged, scope, "OnValueChanged");
            Action<IUIElement>? submit = DataActionRunner.Handler<IUIElement>(def.OnSubmit, scope, "OnSubmit");
            Action<int>? scrolled = DataActionRunner.Handler<int>(def.OnScroll, scope, "OnScroll");
            switch (e)
            {
                case IUICheckbox checkbox:
                    SetIf(changed, h => checkbox.OnValueChanged = h);
                    break;
                case IUITextInput text:
                    SetIf(changed, h => text.OnValueChanged = h);
                    SetIf(submit, h => text.OnSubmit = h);
                    if (def.Validate != null)
                    {
                        text.Validate = value => Validate(ctx, def, scope, path, DataValue.FromString(value));
                    }

                    break;
                case IUINumberInput number:
                    SetIf(changed, h => number.OnValueChanged = h);
                    SetIf(submit, h => number.OnSubmit = h);
                    if (def.Validate != null)
                    {
                        number.Validate = value => Validate(ctx, def, scope, path, DataValue.FromNumber(value));
                    }

                    break;
                case IUIDropdown dropdown:
                    SetIf(changed, h => dropdown.OnValueChanged = h);
                    SetIf(scrolled, h => dropdown.OnScroll = h);
                    break;
                case IUISlider slider:
                    SetIf(changed, h => slider.OnValueChanged = h);
                    break;
                case IUIScrollView scroll:
                    SetIf(scrolled, h => scroll.OnScroll = h);
                    break;
                case IUILabel label:
                    SetIf(DataActionRunner.Handler<string>(def.OnLink, scope, "OnLink"), h => label.OnLink = h);
                    break;
            }
        }

        private static void SetIf<T>(T? handler, Action<T> set) where T : class
        {
            if (handler != null)
            {
                set(handler);
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Validate
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Run an input's <c>Validate</c> expression for a candidate value (<c>event.value</c>): <c>false</c> or a
        /// message rejects it. The error is kept as <c>self.error</c> / <c>el[id].error</c>, and <c>OnInvalid</c> runs
        /// with <c>event.error</c>. An expression that fails to evaluate accepts the value (logged once).
        /// </summary>
        private bool Validate(BuildContext ctx, ElementDefinition def, DataScope scope, DataPath path, DataValue candidate)
        {
            var fields = new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase) { ["value"] = candidate };
            DataValue result = resolver.Evaluate(def.Validate!, scope.WithEvent("Validate", null, fields), out string? error);
            if (error != null)
            {
                UIServices.Log($"[{scope.Owner}] {path.Field("Validate")}: {error}", LogLevel.Trace);
                return true;
            }

            string? message = ValidationMessage(result);
            ctx.Runtime.SetError(scope.Element?.Id ?? ctx.IdOf(def), message);
            if (message != null && def.OnInvalid != null)
            {
                fields["error"] = DataValue.FromString(message);
                try
                {
                    DataActionRunner.Run(def.OnInvalid, scope.WithEvent("OnInvalid", null, fields));
                }
                catch (Exception ex)
                {
                    UIServices.Log($"[{scope.Owner}] {path.Field("OnInvalid")}: {ex.Message}", LogLevel.Warn);
                }
            }

            return message == null;
        }

        /// <summary>Null when a Validate result accepts the value, else the message (<c>false</c> gives a generic one).</summary>
        internal static string? ValidationMessage(DataValue result)
        {
            switch (result.Kind)
            {
                case DataKind.Null:
                    return null;
                case DataKind.String:
                {
                    string text = result.AsString();
                    if (text.Trim().Length == 0)
                    {
                        return null;
                    }

                    return ValueParsers.TryParseBool(text, out bool flag) ? (flag ? null : InvalidMessage) : text;
                }

                default:
                    return result.AsBool() ? null : InvalidMessage;
            }
        }

        private static string InvalidMessage => UIServices.Translation?.Get("data.invalid-value").Default("Invalid value.") ?? "Invalid value.";

        // ---------------------------------------------------------------------------------------------------------
        //  Keys
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// A key handler for <c>Keys: [{ Key, Shift, Ctrl, Alt, Actions }]</c>: the first entry whose key and modifiers
        /// match runs its actions (with <c>event.key</c> ...) and the key counts as handled. Null when there are none.
        /// </summary>
        internal static Func<IUIKeyEvent, bool>? KeyHandler(List<KeyBindingDefinition>? bindings, DataScope scope, DataPath path, DataMessageLog log)
        {
            if (bindings == null || bindings.Count == 0)
            {
                return null;
            }

            var entries = new List<(Keys Key, bool? Shift, bool? Ctrl, bool? Alt, List<ActionDefinition> Actions)>();
            for (int i = 0; i < bindings.Count; i++)
            {
                KeyBindingDefinition? binding = bindings[i];
                DataPath entryPath = path.Index(i, null);
                if (binding == null)
                {
                    continue;
                }

                if (!TryParseKey(binding.Key, out Keys key))
                {
                    log.Error(entryPath.Field("Key"), $"'{binding.Key}' is not a key name (e.g. Enter, Delete, F5, A); the entry is ignored.");
                    continue;
                }

                entries.Add((key, Modifier(binding.Shift, entryPath.Field("Shift"), log), Modifier(binding.Ctrl, entryPath.Field("Ctrl"), log), Modifier(binding.Alt, entryPath.Field("Alt"), log), binding.Actions ?? new List<ActionDefinition>()));
            }

            if (entries.Count == 0)
            {
                return null;
            }

            return e =>
            {
                foreach ((Keys key, bool? shift, bool? ctrl, bool? alt, List<ActionDefinition> actions) in entries)
                {
                    if (key != e.Key || (shift.HasValue && shift.Value != e.Shift) || (ctrl.HasValue && ctrl.Value != e.Ctrl) || (alt.HasValue && alt.Value != e.Alt))
                    {
                        continue;
                    }

                    DataActionRunner.Run(actions, scope.WithEvent("OnKey", e));
                    return true;
                }

                return false;
            };
        }

        /// <summary>Parse an XNA key name (case-insensitive).</summary>
        internal static bool TryParseKey(string? text, out Keys key)
        {
            key = Keys.None;
            string trimmed = text?.Trim() ?? string.Empty;
            return trimmed.Length > 0 && !char.IsDigit(trimmed[0]) && Enum.TryParse(trimmed, ignoreCase: true, out key) && Enum.IsDefined(key) && key != Keys.None
                || (trimmed.Length == 1 && char.IsDigit(trimmed[0]) && Enum.TryParse("D" + trimmed, out key));
        }

        private static bool? Modifier(string? raw, DataPath path, DataMessageLog log)
        {
            if (raw == null)
            {
                return null;
            }

            if (ValueParsers.TryParseBool(raw, out bool value))
            {
                return value;
            }

            log.Warn(path, $"'{raw}' is not true or false; the modifier is ignored.");
            return null;
        }

        // ---------------------------------------------------------------------------------------------------------
        //  OnUpdate
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// An update handler that runs <paramref name="actions"/> every <c>UpdateIntervalMs</c> (every tick when 0),
        /// with <c>event.elapsed</c> the milliseconds since the last run. Null when there are no actions.
        /// </summary>
        internal static Action<T, double>? UpdateHandler<T>(List<ActionDefinition>? actions, string? intervalRaw, DataScope scope, DataPath path, PropertyApplier applier)
        {
            if (actions == null || actions.Count == 0)
            {
                return null;
            }

            int interval = Math.Max(0, applier.Initial(intervalRaw, ValueParsers.Int, 0, scope, path));
            double accumulated = 0;
            return (_, elapsed) =>
            {
                accumulated += elapsed;
                if (accumulated < interval)
                {
                    return;
                }

                double since = accumulated;
                accumulated = 0;
                DataActionRunner.Run(actions, scope.WithEvent("OnUpdate", since));
            };
        }
    }
}
