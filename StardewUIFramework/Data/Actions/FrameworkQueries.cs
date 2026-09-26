using System;
using StardewValley;
using StardewValley.Delegates;
using UIFramework.Data.Expressions;
using UIFramework.Data.State;

namespace UIFramework.Data.Actions
{
    /// <summary>
    /// The framework's game state queries:
    /// <list type="bullet">
    ///   <item><c>6135.UIFramework_MENU_OPEN &lt;owner/menu&gt;</c>: that menu (data or C#) is open on the current screen;</item>
    ///   <item><c>6135.UIFramework_HUD_VISIBLE &lt;owner/hud&gt;</c>: that data HUD is shown for the current player;</item>
    ///   <item><c>6135.UIFramework_STATE &lt;key&gt; &lt;value&gt;+</c>: the state value equals any of the values;</item>
    ///   <item><c>6135.UIFramework_STATE_NUMBER &lt;key&gt; &lt;min&gt; [max]</c>: the state value is a number in the range.</item>
    /// </list>
    /// Keys work unqualified inside a data UI's actions (<c>menu.x</c>) and qualified anywhere (<c>session[owner].x</c>).
    /// </summary>
    internal static class FrameworkQueries
    {
        internal const string MenuOpen = "6135.UIFramework_MENU_OPEN";
        internal const string HudVisible = "6135.UIFramework_HUD_VISIBLE";
        internal const string State = "6135.UIFramework_STATE";
        internal const string StateNumber = "6135.UIFramework_STATE_NUMBER";

        /// <summary>Register the queries (once, from <see cref="ModEntry.Entry"/>).</summary>
        internal static void Register(DataService data)
        {
            GameStateQuery.Register(MenuOpen, (string[] query, GameStateQueryContext context) =>
            {
                if (!ArgUtility.TryGet(query, 1, out string key, out string error, allowBlank: false, "string menuKey"))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error, null);
                }

                return data.IsOpen(key);
            });

            GameStateQuery.Register(HudVisible, (string[] query, GameStateQueryContext context) =>
            {
                if (!ArgUtility.TryGet(query, 1, out string key, out string error, allowBlank: false, "string hudKey"))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error, null);
                }

                return data.IsHudShown(key);
            });

            GameStateQuery.Register(State, (string[] query, GameStateQueryContext context) =>
            {
                if (!TryRead(data, query, out DataValue value, out string error))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error, null);
                }

                for (int i = 2; i < query.Length; i++)
                {
                    if (DataValue.LooseEquals(value, StateAddress.Infer(query[i])) || string.Equals(value.AsString(), query[i], StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                return false;
            });

            GameStateQuery.Register(StateNumber, (string[] query, GameStateQueryContext context) =>
            {
                if (!TryRead(data, query, out DataValue value, out string error)
                    || !ArgUtility.TryGetFloat(query, 2, out float min, out error, "float min")
                    || !ArgUtility.TryGetOptionalFloat(query, 3, out float max, out error, float.MaxValue, "float max"))
                {
                    return GameStateQuery.Helpers.ErrorResult(query, error, null);
                }

                double number = value.AsNumber();
                return number >= min && number <= max;
            });
        }

        private static bool TryRead(DataService data, string[] query, out DataValue value, out string error)
        {
            value = DataValue.Null;
            if (!ArgUtility.TryGet(query, 1, out string key, out error, allowBlank: false, "string key"))
            {
                return false;
            }

            DataScope? scope = DataActionRunner.Ambient;
            if (!StateAddress.TryParse(key, scope, allowBare: scope != null, out StateAddress address, out error))
            {
                return false;
            }

            value = data.State.Read(address);
            return true;
        }
    }
}
