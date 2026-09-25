using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// Applies the <c>Decorate</c> operations of a contribution (v1.7) to another mod's menu, through the contributor's
    /// API instance (it is registered as the menu's decorator, <c>OnScreenBuilt</c>). Every edit records how to undo
    /// it, so the next open (or a reload) first restores the tree and then applies the current operations: edits never
    /// pile up on C# menus whose tree survives between opens. An element inside a sealed subtree refuses every edit
    /// (logged as an error once per operation); added elements belong to the contributor.
    /// </summary>
    internal sealed class DecorationApplier
    {
        private readonly DataBuilder builder;
        private readonly IValueResolver resolver;

        internal DecorationApplier(DataBuilder builder, IValueResolver resolver)
        {
            this.builder = builder;
            this.resolver = resolver;
        }

        /// <summary>Apply every operation of <paramref name="runtime"/> to <paramref name="menu"/>, recording undo steps in <paramref name="undo"/>; live values register in <paramref name="group"/>.</summary>
        internal void Apply(DataContributionRuntime runtime, UIMenu menu, StardewUIApi api, ConsumerContext contributor, List<Action> undo, RefresherGroup group)
        {
            List<DecorationOp>? ops = runtime.Definition.Decorate;
            if (ops == null)
            {
                return;
            }

            var log = new DataMessageLog();
            var applier = new PropertyApplier(resolver, group, log);
            DataScope scope = DataScope.ForRuntime(runtime, menu);
            for (int i = 0; i < ops.Count; i++)
            {
                DecorationOp? op = ops[i];
                string? kind = DecorationOps.Canonical(op?.Op);
                if (op == null || kind == null)
                {
                    continue; // reported by the validator
                }

                DataPath path = runtime.Path.Field("Decorate").Index(i, op.Target);
                try
                {
                    ApplyOne(kind, op, runtime, menu, api, contributor, undo, applier, scope, path);
                }
                catch (InvalidOperationException ex)
                {
                    Report(runtime, path, $"{kind} '{op.Target}' was refused: {ex.Message}", LogLevel.Error);
                }
                catch (Exception ex)
                {
                    Report(runtime, path, $"{kind} '{op.Target}' failed: {ex.Message}", LogLevel.Error);
                }
            }

            ReportBuild(runtime, log);
        }

        /// <summary>Log the problems of building / applying data for <paramref name="runtime"/> (each once per contribution version).</summary>
        internal static void ReportBuild(DataContributionRuntime runtime, DataMessageLog log)
        {
            foreach (DataMessage message in log.Items)
            {
                if (message.Severity != DataSeverity.Info && runtime.Reported.Add(message.ToString()))
                {
                    UIServices.Log($"[{runtime.Owner}] {message}", message.Severity == DataSeverity.Error ? LogLevel.Error : LogLevel.Warn);
                }
            }
        }

        private void ApplyOne(string kind, DecorationOp op, DataContributionRuntime runtime, UIMenu menu, StardewUIApi api, ConsumerContext contributor, List<Action> undo, PropertyApplier applier, DataScope scope, DataPath path)
        {
            UIElement? target = Find(menu, op.Target);
            if (target == null)
            {
                Report(runtime, path, $"{menu} has no element '{op.Target}'; {kind} is skipped.", LogLevel.Warn);
                return;
            }

            // sealed subtrees refuse everything (the owner of the menu is never restricted)
            Sealing.RequireWriteAccess(target, contributor, isDecorator: true);
            IUIElement pub = target;
            switch (kind)
            {
                case DecorationOps.Hide:
                case DecorationOps.Show:
                {
                    bool old = pub.Visible;
                    pub.Visible = kind == DecorationOps.Show;
                    undo.Add(() => pub.Visible = old);
                    break;
                }

                case DecorationOps.Set:
                    if (op.Fields != null)
                    {
                        foreach ((string field, string? raw) in op.Fields)
                        {
                            if (raw == null)
                            {
                                continue;
                            }

                            Action? restore = SetField(target, field?.Trim() ?? string.Empty, raw, scope, path.Field("Fields").Field(field ?? "?"), applier);
                            if (restore == null)
                            {
                                Report(runtime, path.Field("Fields").Field(field ?? "?"), $"'{field}' cannot be set on '{target.Id}' ({target.GetType().Name}); Set accepts {string.Join(", ", DecorationOps.SetFields)}.", LogLevel.Warn);
                            }
                            else
                            {
                                undo.Add(restore);
                            }
                        }
                    }

                    break;

                case DecorationOps.InsertBefore:
                case DecorationOps.InsertAfter:
                {
                    UIContainer parent = target.ParentElement ?? throw new InvalidOperationException($"'{target.Id}' has no parent to insert into.");
                    Sealing.RequireWriteAccess(parent, contributor, isDecorator: true);
                    int index = IndexOf(parent, target) + (kind == DecorationOps.InsertAfter ? 1 : 0);
                    Insert(runtime, api, contributor, parent, index, op, applier, scope, path, undo);
                    break;
                }

                case DecorationOps.Append:
                {
                    if (target is not UIContainer container)
                    {
                        throw new InvalidOperationException($"'{target.Id}' is not a container.");
                    }

                    Insert(runtime, api, contributor, container, container.Children.Count, op, applier, scope, path, undo);
                    break;
                }

                case DecorationOps.Replace:
                {
                    UIContainer parent = target.ParentElement ?? throw new InvalidOperationException($"'{target.Id}' has no parent.");
                    int index = IndexOf(parent, target);
                    parent.Remove(target);
                    undo.Add(() => parent.Insert(index, target));
                    Insert(runtime, api, contributor, parent, index, op, applier, scope, path, undo);
                    break;
                }

                case DecorationOps.Remove:
                {
                    UIContainer parent = target.ParentElement ?? throw new InvalidOperationException($"'{target.Id}' has no parent.");
                    int index = IndexOf(parent, target);
                    parent.Remove(target);
                    undo.Add(() => parent.Insert(index, target));
                    break;
                }

                case DecorationOps.Move:
                    Move(menu, target, op, contributor, undo);
                    break;
            }
        }

        /// <summary>Move <paramref name="target"/> before / after another element or to the end of a container.</summary>
        private static void Move(UIMenu menu, UIElement target, DecorationOp op, ConsumerContext contributor, List<Action> undo)
        {
            string? anchorId = op.Before ?? op.After ?? op.Into;
            UIElement anchor = Find(menu, anchorId) ?? throw new InvalidOperationException($"Move needs an existing Before, After or Into element ('{anchorId}' was not found).");
            UIContainer destination = op.Into != null
                ? anchor as UIContainer ?? throw new InvalidOperationException($"'{anchor.Id}' is not a container.")
                : anchor.ParentElement ?? throw new InvalidOperationException($"'{anchor.Id}' has no parent.");
            if (anchor == target || (target is UIContainer self && destination.IsSelfOrDescendantOf(self)))
            {
                throw new InvalidOperationException($"'{target.Id}' cannot move into itself.");
            }

            Sealing.RequireWriteAccess(destination, contributor, isDecorator: true);
            UIContainer oldParent = target.ParentElement ?? throw new InvalidOperationException($"'{target.Id}' has no parent.");
            int oldIndex = IndexOf(oldParent, target);
            oldParent.Remove(target);
            int index = op.Into != null ? destination.Children.Count : IndexOf(destination, anchor) + (op.After != null && op.Before == null ? 1 : 0);
            destination.Insert(index, target);
            undo.Add(() => oldParent.Insert(oldIndex, target));
        }

        /// <summary>Build the operation's children through the contributor's facade and place them at <paramref name="index"/> of <paramref name="parent"/>.</summary>
        private void Insert(DataContributionRuntime runtime, StardewUIApi api, ConsumerContext contributor, UIContainer parent, int index, DecorationOp op, PropertyApplier applier, DataScope scope, DataPath path, List<Action> undo)
        {
            if (op.Children is not { Count: > 0 })
            {
                return;
            }

            int before = parent.Children.Count;
            builder.BuildTree(api, runtime, applier, parent, op.Children, scope, path.Field("Children"));
            List<UIElement> created = parent.Children.Skip(before).ToList();
            for (int k = 0; k < created.Count; k++)
            {
                UIElement element = created[k];
                element.Contributor = contributor; // the contributor's own subtree (its guard, its edits)
                parent.Insert(index + k, element);
            }

            undo.Add(() =>
            {
                foreach (UIElement element in created)
                {
                    element.ParentElement?.Remove(element);
                }
            });
        }

        /// <summary>Set one member (possibly live); returns how to restore its previous value, or null when the element has no such member.</summary>
        private static Action? SetField(UIElement e, string field, string raw, DataScope scope, DataPath path, PropertyApplier a)
        {
            IUIElement pub = e;
            string? canonical = DecorationOps.SetFields.FirstOrDefault(f => string.Equals(f, field, StringComparison.OrdinalIgnoreCase));
            switch (canonical)
            {
                case "Visible":
                {
                    bool old = pub.Visible;
                    a.Apply(raw, ValueParsers.Bool, scope, path, v => pub.Visible = v);
                    return () => pub.Visible = old;
                }

                case "Enabled":
                {
                    bool old = pub.Enabled;
                    a.Apply(raw, ValueParsers.Bool, scope, path, v => pub.Enabled = v);
                    return () => pub.Enabled = old;
                }

                case "Text":
                {
                    Func<string>? text = a.Text(raw, scope, path);
                    switch (e)
                    {
                        case IUILabel label when text != null:
                        {
                            Func<string> old = label.Text;
                            label.Text = text;
                            return () => label.Text = old;
                        }

                        case IUIButton button when text != null:
                        {
                            Func<string> old = button.Text;
                            button.Text = text;
                            return () => button.Text = old;
                        }

                        default:
                            return null;
                    }
                }

                case "Tooltip":
                {
                    Func<string> old = pub.Tooltip;
                    Func<string>? text = a.Text(raw, scope, path);
                    pub.Tooltip = text!;
                    return () => pub.Tooltip = old;
                }

                case "TooltipTitle":
                {
                    Func<string> old = pub.TooltipTitle;
                    Func<string>? text = a.Text(raw, scope, path);
                    pub.TooltipTitle = text!;
                    return () => pub.TooltipTitle = old;
                }

                case "Tag":
                {
                    object old = pub.Tag;
                    a.Apply(raw, ValueParsers.Text, scope, path, v => pub.Tag = v);
                    return () => pub.Tag = old;
                }

                case "Width":
                {
                    int? old = pub.Width;
                    a.Apply(raw, ValueParsers.OptionalInt, scope, path, v => pub.Width = v);
                    return () => pub.Width = old;
                }

                case "Height":
                {
                    int? old = pub.Height;
                    a.Apply(raw, ValueParsers.OptionalInt, scope, path, v => pub.Height = v);
                    return () => pub.Height = old;
                }

                case "Margin":
                {
                    int[] old = { pub.MarginLeft, pub.MarginTop, pub.MarginRight, pub.MarginBottom };
                    a.Apply(raw, ValueParsers.Margin, scope, path, v => pub.SetMargin(v[0], v[1], v[2], v[3]));
                    return () => pub.SetMargin(old[0], old[1], old[2], old[3]);
                }

                case "HorizontalAlign":
                {
                    UIAlign old = pub.HorizontalAlign;
                    a.Apply(raw, ValueParsers.Align, scope, path, v => pub.HorizontalAlign = v);
                    return () => pub.HorizontalAlign = old;
                }

                case "VerticalAlign":
                {
                    UIAlign old = pub.VerticalAlign;
                    a.Apply(raw, ValueParsers.Align, scope, path, v => pub.VerticalAlign = v);
                    return () => pub.VerticalAlign = old;
                }

                case "Color" when e is IUILabel label:
                {
                    Color? old = label.Color;
                    a.Apply(raw, ValueParsers.ColorValue, scope, path, v => label.Color = v);
                    return () => label.Color = old;
                }

                case "Font" when e is IUILabel label:
                {
                    UIFont old = label.Font;
                    a.Apply(raw, ValueParsers.Font, scope, path, v => label.Font = v);
                    return () => label.Font = old;
                }

                case "Font" when e is IUIButton button:
                {
                    UIFont old = button.Font;
                    a.Apply(raw, ValueParsers.Font, scope, path, v => button.Font = v);
                    return () => button.Font = old;
                }

                default:
                    return null;
            }
        }

        private static UIElement? Find(UIMenu menu, string? id)
        {
            return string.IsNullOrWhiteSpace(id) ? null : menu.Root.FindById(id.Trim());
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

            return children.Count;
        }

        /// <summary>Log a run-time problem once per contribution version.</summary>
        private static void Report(DataContributionRuntime runtime, DataPath path, string message, LogLevel level)
        {
            if (runtime.Reported.Add(path + "|" + message))
            {
                UIServices.Log($"[{runtime.Owner}] {path}: {message}", level);
            }
        }
    }
}
