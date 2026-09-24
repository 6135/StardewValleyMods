using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.TokenizableStrings;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.State;

namespace UIFramework.Data.Expressions
{
    /// <summary>
    /// The game and UI functions of data expressions, registered into <see cref="FunctionRegistry.Default"/> by the
    /// data layer:
    /// <list type="bullet">
    ///   <item><c>gsq(query)</c>: a game state query for the current player (volatile: cached once per tick, so <c>RANDOM</c> flickers);</item>
    ///   <item><c>token(text)</c>: a tokenizable string (<c>[LocalizedText ...]</c>, <c>[ItemName ...]</c>...) (volatile);</item>
    ///   <item><c>loc(key)</c>: a translation from a game asset (<c>Strings/UI:Key</c>);</item>
    ///   <item><c>isOpen(owner/menu)</c>: a framework menu is open on this screen (volatile);</item>
    ///   <item><c>focused(id)</c> / <c>hovered(id)</c>: an element of the current UI has focus / the cursor (volatile);</item>
    ///   <item><c>bounds(id)</c>: <c>{x, y, width, height}</c> of an element of the current UI (volatile);</item>
    ///   <item><c>itemName(id)</c>: the display name of a (qualified or unqualified) item id.</item>
    /// </list>
    /// </summary>
    internal static class GameFunctions
    {
        /// <summary>Register the functions into <paramref name="registry"/>.</summary>
        internal static void Register(FunctionRegistry registry, DataService data)
        {
            registry.Register("gsq", 1, 1, (c, a) =>
            {
                string query = a[0].AsString();
                return DataValue.FromBool(Actions.DataActionRunner.CheckCondition(query));
            }, isVolatile: true);

            registry.Register("token", 1, 1, (c, a) => c.Text(TokenParser.ParseText(a[0].AsString()) ?? string.Empty), isVolatile: true);

            registry.Register("loc", 1, 1, (c, a) =>
            {
                string key = a[0].AsString();
                try
                {
                    return c.Text(Game1.content.LoadString(key) ?? key);
                }
                catch (Exception)
                {
                    return DataValue.FromString(key);
                }
            });

            registry.Register("isOpen", 1, 1, (c, a) => DataValue.FromBool(data.IsOpen(a[0].AsString())), isVolatile: true);
            registry.Register("focused", 0, 1, (c, a) => DataValue.FromBool(Element(c, a)?.IsFocused ?? false), isVolatile: true);
            registry.Register("hovered", 0, 1, (c, a) => DataValue.FromBool(Element(c, a)?.IsHovered ?? false), isVolatile: true);
            registry.Register("bounds", 0, 1, (c, a) =>
            {
                UIElement? element = Element(c, a);
                if (element == null)
                {
                    return DataValue.Null;
                }

                Rectangle r = ((IUIElement)element).Bounds;
                return DataValue.Opaque(new Dictionary<string, DataValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["x"] = DataValue.FromNumber(r.X),
                    ["y"] = DataValue.FromNumber(r.Y),
                    ["width"] = DataValue.FromNumber(r.Width),
                    ["height"] = DataValue.FromNumber(r.Height)
                });
            }, isVolatile: true);

            registry.Register("itemName", 1, 1, (c, a) =>
            {
                string id = a[0].AsString().Trim();
                return id.Length == 0 ? DataValue.EmptyString : c.Text(ItemRegistry.GetDataOrErrorItem(id).DisplayName ?? id);
            });
        }

        /// <summary>The element named by the first argument in the evaluating UI (no argument: the scope's own element).</summary>
        private static UIElement? Element(ExpressionContext context, ReadOnlySpan<DataValue> args)
        {
            if (context.Scope is not DataScope scope)
            {
                return null;
            }

            return args.Length == 0 ? scope.Element : ScopeRoots.FindElement(scope, args[0].AsString());
        }
    }
}
