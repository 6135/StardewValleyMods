using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;

namespace UIFramework.Core.Export
{
    internal sealed partial class JsonEmitter
    {
        // ---------------------------------------------------------------------------------------------------------
        //  Writer (JSON with // comments)
        // ---------------------------------------------------------------------------------------------------------

        private void Write(JToken token, int depth)
        {
            switch (token)
            {
                case JRaw raw:
                    sb.Append(raw.Text);
                    break;
                case JObj obj:
                    WriteMembers('{', '}', obj.Members, depth);
                    break;
                case JArr arr:
                    WriteMembers('[', ']', arr.Members, depth);
                    break;
            }
        }

        private void WriteMembers(char open, char close, List<JMember> members, int depth)
        {
            if (members.Count == 0)
            {
                sb.Append(open).Append(close);
                return;
            }

            sb.Append(open).Append('\n');
            int lastValue = members.FindLastIndex(m => m.Value != null);
            for (int i = 0; i < members.Count; i++)
            {
                JMember m = members[i];
                sb.Append(' ', (depth + 1) * 2);
                if (m.Value == null)
                {
                    sb.Append("// ").Append(m.Comment).Append('\n');
                    continue;
                }

                if (m.Key != null)
                {
                    sb.Append(JsonConvert.ToString(m.Key)).Append(": ");
                }

                Write(m.Value, depth + 1);
                if (i < lastValue)
                {
                    sb.Append(',');
                }

                if (m.Comment != null)
                {
                    sb.Append(" // ").Append(m.Comment);
                }

                sb.Append('\n');
            }

            sb.Append(' ', depth * 2).Append(close);
        }

        private abstract class JToken
        {
        }

        /// <summary>An already-serialized scalar.</summary>
        private sealed class JRaw : JToken
        {
            internal JRaw(string text)
            {
                Text = text;
            }

            internal string Text { get; }
        }

        /// <summary>A member (object: keyed; array: <see cref="Key"/> null) or a comment-only line (<see cref="Value"/> null).</summary>
        private sealed class JMember
        {
            internal string? Key { get; init; }
            internal JToken? Value { get; init; }
            internal string? Comment { get; init; }
            internal int Rank { get; init; }
        }

        private sealed class JObj : JToken
        {
            internal List<JMember> Members { get; private set; } = new();

            internal void Add(string key, JToken value, int rank, string? comment = null)
            {
                Members.Add(new JMember { Key = key, Value = value, Rank = rank, Comment = comment });
            }

            internal void Comment(string comment, int rank)
            {
                Members.Add(new JMember { Comment = comment, Rank = rank });
            }

            /// <summary>Members ordered by rank (stable).</summary>
            internal JObj Sorted()
            {
                Members = Members.OrderBy(m => m.Rank).ToList();
                return this;
            }
        }

        private sealed class JArr : JToken
        {
            internal List<JMember> Members { get; } = new();

            internal void Add(JToken value)
            {
                Members.Add(new JMember { Value = value });
            }

            internal void Comment(string comment)
            {
                Members.Add(new JMember { Comment = comment });
            }
        }
    }
}
