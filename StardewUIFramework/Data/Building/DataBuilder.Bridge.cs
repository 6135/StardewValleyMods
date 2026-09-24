using System.Linq;
using UIFramework.Api;
using UIFramework.Components;
using UIFramework.Core;
using UIFramework.Data.Bridge;
using UIFramework.Data.Expressions;
using UIFramework.Data.Loading;
using UIFramework.Data.Model;
using UIFramework.Hosting;

namespace UIFramework.Data.Building
{
    /// <summary>
    /// The C# bridge in data trees (v1.6): <c>Composite</c> elements (and custom tags) over composites defined in C#,
    /// with data arguments and a <c>ContentTarget</c> for their children; <c>DrawExtra</c> / <c>DrawOverlay</c> draw
    /// hooks; and <c>Form</c>s over a model exposed from C#.
    /// </summary>
    internal sealed partial class DataBuilder
    {
        /// <summary>
        /// A composite instance whose arguments are data values (<see cref="DataArgument"/>), converted on each read by the
        /// builder. The bag also carries the instance's children and build context (<see cref="InstancePayload"/>) so a
        /// data composite (v1.7) can route them into its Outlets on every (re)build. A <c>Composite</c> name that is an
        /// expression (<c>"${menu.page}"</c>) instantiates another composite in place whenever it changes.
        /// </summary>
        private IUIElement CreateComposite(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            ValueSource<string>? nameSource = ctx.Applier.Source(def.Composite ?? string.Empty, ValueParsers.Text, path.Field("Composite"));
            string name = (nameSource?.Get(scope) ?? string.Empty).Trim();
            if (name.Length == 0)
            {
                name = NoComposite;
            }
            else if (!ctx.Api.HasComposite(name))
            {
                string? suggestion = DataValidator.Suggest(name, ctx.Api.ListComposites());
                ctx.Log.Warn(path.Field("Composite"), $"no composite '{name}' is defined (yet){(suggestion != null ? $"; did you mean '{suggestion}'?" : string.Empty)}. The element stays empty until a C# mod or the Composites asset defines it.");
            }

            var args = new CompositeArgs();
            if (def.Args != null)
            {
                foreach ((string key, Newtonsoft.Json.Linq.JToken? token) in def.Args)
                {
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        args.SetData(key.Trim(), new DataArgument(key.Trim(), token, scope));
                    }
                }
            }

            var childGroup = new RefresherGroup(ctx.Runtime.Owner, ctx.IdOf(def) + ".children");
            ctx.Applier.AddRefresher(childGroup);
            var payload = new InstancePayload(ctx, scope, def.Children, path, childGroup);
            args.DataPayload = payload;

            IUIComposite composite = ctx.Api.AddComposite(parent, ctx.IdOf(def), name, args);
            if (nameSource is { IsDynamic: true })
            {
                ctx.Applier.AddRefresher(new CompositeNameRefresher(this, ctx, (Composite)composite, def, scope, path, nameSource, payload));
            }

            return composite;
        }

        /// <summary>
        /// Where a composite element's data children go: its <c>ContentTarget</c> (an id inside the composite, also tried
        /// as <c>&lt;compositeId&gt;.&lt;target&gt;</c>), else the host of its first custom component (<c>&lt;id&gt;.host</c>),
        /// else the composite itself.
        /// </summary>
        private static IUIContainer ContentTargetOf(Composite composite, ElementDefinition def, BuildContext ctx, DataPath path)
        {
            if (!string.IsNullOrWhiteSpace(def.ContentTarget))
            {
                string target = def.ContentTarget.Trim();
                UIElement? found = composite.FindById(target) ?? composite.FindById(composite.Id + "." + target);
                if (found is UIContainer container)
                {
                    return found is CustomHostAdapter adapter ? adapter.Host : container;
                }

                ctx.Log.Error(path.Field("ContentTarget"), found == null
                    ? $"composite '{composite.CompositeName}' has no element '{target}'; the children go into the composite itself."
                    : $"'{target}' is not a container; the children go into the composite itself.");
                return composite;
            }

            CustomHostAdapter? host = composite.SelfAndDescendants().OfType<CustomHostAdapter>().FirstOrDefault();
            return host != null ? host.Host : composite;
        }

        /// <summary><c>DrawExtra</c> / <c>DrawOverlay</c>: draw hooks registered in C#, looked up when drawn.</summary>
        private void ApplyDrawHooks(IUIElement e, ElementDefinition def, DataScope scope, DataPath path, DataMessageLog log)
        {
            HookRegistry? hooks = UIServices.Hooks;
            if (hooks == null || (def.DrawExtra == null && def.DrawOverlay == null))
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(def.DrawExtra))
            {
                string key = HookRegistry.Qualify(resolver.Interpolate(def.DrawExtra, scope), scope.Owner);
                NoteMissingDraw(hooks, key, path.Field("DrawExtra"), log);
                e.OnDrawExtra = hooks.DrawCallback(key, scope);
            }

            if (!string.IsNullOrWhiteSpace(def.DrawOverlay))
            {
                string key = HookRegistry.Qualify(resolver.Interpolate(def.DrawOverlay, scope), scope.Owner);
                NoteMissingDraw(hooks, key, path.Field("DrawOverlay"), log);
                e.OnDrawOverlay = hooks.DrawCallback(key, scope);
            }
        }

        private static void NoteMissingDraw(HookRegistry hooks, string key, DataPath path, DataMessageLog log)
        {
            if (!hooks.HasDrawHook(key))
            {
                log.Info(path, $"no draw hook '{key}' is registered yet (RegisterDrawHook); nothing is drawn until one is.");
            }
        }

        /// <summary>
        /// A <c>Form</c> over a C# model (<c>"Model": "settings"</c>, <c>"model.settings.Sub"</c>, <c>"model[ModId/settings]"</c>):
        /// the same reflected form as <c>AddForm</c> (attributes, sections, validation), editing the object directly.
        /// </summary>
        private IUIElement CreateModelForm(BuildContext ctx, IUIContainer parent, ElementDefinition def, DataScope scope, DataPath path)
        {
            string raw = def.Model!.Trim();
            string text = raw.StartsWith("model.", System.StringComparison.Ordinal) || raw.StartsWith("model[", System.StringComparison.Ordinal) || raw.StartsWith('@') || raw.StartsWith('.')
                ? raw
                : "model." + raw;
            object? model = null;
            if (BindTarget.TryParse(text, scope, allowBare: false, out BindTarget? target, out string error))
            {
                DataValue value = target.Read();
                model = value.Kind == DataKind.Object ? value.AsObject() : null;
                error = model == null ? $"nothing is exposed as '{raw}' (ExposeModel), or it is not an object" : string.Empty;
            }

            if (model == null)
            {
                ctx.Log.Warn(path.Field("Model"), $"{error}; the form is empty until the model is exposed (the menu then rebuilds).");
                return ctx.Api.AddFormInternal(parent, ctx.IdOf(def), new DataFormModel(scope.MenuKey + "#" + ctx.IdOf(def)), System.Array.Empty<FormProperty>());
            }

            return ctx.Api.AddForm(parent, ctx.IdOf(def), model);
        }
    }
}
