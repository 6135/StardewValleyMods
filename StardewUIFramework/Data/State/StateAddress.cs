using System;
using System.Collections.Generic;
using System.Text;
using UIFramework.Data.Expressions;

namespace UIFramework.Data.State
{
    /// <summary>The writable state scopes.</summary>
    internal enum StateScope
    {
        /// <summary><c>menu.x</c>: per menu (or HUD) and screen; survives menu replacement and hot reload.</summary>
        Menu,

        /// <summary><c>session.x</c>: per owner and screen, until the return to title.</summary>
        Session,

        /// <summary><c>player.x</c>: <c>Game1.player.modData["&lt;owner&gt;/x"]</c> (saved, synced).</summary>
        Player,

        /// <summary><c>stat.x</c>: the current player's stats (vanilla <c>PLAYER_STAT</c> / <c>IncrementStat</c>).</summary>
        Stat,

        /// <summary><c>config.x</c>: per owner, global across saves (<c>data/&lt;owner&gt;.json</c>).</summary>
        Config
    }

    /// <summary>
    /// Where a state value lives: its scope, the key of its container (<c>owner/menu</c> for <see cref="StateScope.Menu"/>,
    /// the owner id for session / player / config, empty for stats) and its name (dotted names are allowed:
    /// <c>menu.settings.day</c> is the cell <c>settings.day</c>).
    /// </summary>
    internal readonly record struct StateAddress(StateScope Scope, string Container, string Name)
    {
        /// <summary>The owner mod id of the value (empty for stats).</summary>
        internal string Owner
        {
            get
            {
                if (Scope != StateScope.Menu)
                {
                    return Container;
                }

                int slash = Container.IndexOf('/');
                return slash > 0 ? Container.Substring(0, slash) : Container;
            }
        }

        /// <summary>The qualified form (<c>menu[owner/menu].x</c>, <c>session[owner].x</c>, <c>stat.x</c>) that works from anywhere.</summary>
        public override string ToString()
        {
            string root = RootName(Scope);
            return Scope == StateScope.Stat ? root + "." + Name : $"{root}[{Container}].{Name}";
        }

        /// <summary>The expression root of a scope (<c>menu</c>, <c>session</c>...).</summary>
        internal static string RootName(StateScope scope) => scope switch
        {
            StateScope.Menu => "menu",
            StateScope.Session => "session",
            StateScope.Player => "player",
            StateScope.Stat => "stat",
            _ => "config"
        };

        /// <summary>The scope named by an expression root, if it is a writable state scope.</summary>
        internal static bool TryGetScope(string root, out StateScope scope)
        {
            switch (root)
            {
                case "menu":
                    scope = StateScope.Menu;
                    return true;
                case "session":
                    scope = StateScope.Session;
                    return true;
                case "player":
                    scope = StateScope.Player;
                    return true;
                case "stat":
                    scope = StateScope.Stat;
                    return true;
                case "config":
                    scope = StateScope.Config;
                    return true;
                default:
                    scope = StateScope.Menu;
                    return false;
            }
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Parsing
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>
        /// Parse a state key as written in <c>Bind</c>, <c>Out</c>, <c>Watch</c>, trigger actions, queries, tokens and
        /// the console: <c>menu.x</c>, <c>menu[owner/menu].x</c>, <c>session[owner].x</c>, <c>player.x</c>,
        /// <c>stat.x</c>, <c>config[owner].x</c>, <c>.x</c> (relative to the <c>With</c> scope) and, when
        /// <paramref name="allowBare"/>, a bare <c>x</c> (= <c>menu.x</c>). Unqualified forms need
        /// <paramref name="context"/> for their owner / menu.
        /// </summary>
        internal static bool TryParse(string? text, DataScope? context, bool allowBare, out StateAddress address, out string error)
        {
            address = default;
            text = text?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                error = "the state key is empty.";
                return false;
            }

            CompiledExpression expression = CompiledExpression.Compile(text);
            if (expression.Root is not PathNode { StaticPath: { } path })
            {
                error = expression.Error != null ? $"'{text}' is not a state key: {expression.Error}" : $"'{text}' is not a state key (expected e.g. menu.name or session[owner].name).";
                return false;
            }

            return TryFromPath(context?.ExpandRelative(path) ?? path, context, allowBare, out address, out error);
        }

        /// <summary>The address of an identifier path (see <see cref="TryParse"/>); a relative path must already be expanded.</summary>
        internal static bool TryFromPath(IReadOnlyList<PathSegment> path, DataScope? context, bool allowBare, out StateAddress address, out string error)
        {
            address = default;
            error = string.Empty;
            if (path.Count == 0)
            {
                error = "the state key is empty.";
                return false;
            }

            string root = path[0].Key;
            if (!TryGetScope(root, out StateScope scope))
            {
                if (root == Parser.RelativeRoot)
                {
                    error = "'.name' keys are only valid inside an element with a With scope.";
                    return false;
                }

                if (!allowBare || path[0].IsIndex)
                {
                    error = $"'{PathSegment.Format(path)}' is not a state key (expected menu., session., player., stat. or config.).";
                    return false;
                }

                if (context?.StateKey == null)
                {
                    error = $"'{root}' needs a menu (write menu[owner/menu].{root} outside a menu).";
                    return false;
                }

                address = new StateAddress(StateScope.Menu, context.StateKey, JoinName(path, 0));
                return true;
            }

            int nameStart = 1;
            string? container;
            if (path.Count > 1 && path[1].IsIndex)
            {
                container = path[1].Key.Trim();
                nameStart = 2;
                if (scope == StateScope.Menu && !container.Contains('/'))
                {
                    error = $"'{PathSegment.Format(path)}': the menu qualifier must be '<owner>/<menu>'.";
                    return false;
                }
            }
            else
            {
                container = scope switch
                {
                    StateScope.Stat => string.Empty,
                    StateScope.Menu => context?.StateKey,
                    _ => context?.Owner
                };
            }

            if (scope == StateScope.Stat)
            {
                container = string.Empty;
            }

            if (container == null)
            {
                string example = scope == StateScope.Menu ? "menu[owner/menu]" : root + "[owner]";
                error = $"'{PathSegment.Format(path)}' needs an owner outside a UI; write {example}.{JoinName(path, nameStart)}.";
                return false;
            }

            string name = JoinName(path, nameStart);
            if (name.Length == 0)
            {
                error = $"'{PathSegment.Format(path)}' has no value name.";
                return false;
            }

            address = new StateAddress(scope, container, name);
            return true;
        }

        /// <summary>Join <paramref name="path"/> from <paramref name="start"/> with dots (index steps included as their key).</summary>
        internal static string JoinName(IReadOnlyList<PathSegment> path, int start)
        {
            if (start >= path.Count)
            {
                return string.Empty;
            }

            if (start == path.Count - 1)
            {
                return path[start].Key;
            }

            var builder = new StringBuilder();
            for (int i = start; i < path.Count; i++)
            {
                if (i > start)
                {
                    builder.Append('.');
                }

                builder.Append(path[i].Key);
            }

            return builder.ToString();
        }

        /// <summary>Infer a typed value from text (state defaults, <c>SetState</c>, modData, config): numbers, <c>true</c> / <c>false</c>, else text.</summary>
        internal static DataValue Infer(string? text)
        {
            if (text == null)
            {
                return DataValue.Null;
            }

            string trimmed = text.Trim();
            if (DataValue.TryParseNumber(trimmed, out double number))
            {
                return DataValue.FromNumber(number);
            }

            if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                return DataValue.True;
            }

            if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return DataValue.False;
            }

            return DataValue.FromString(text);
        }
    }
}
