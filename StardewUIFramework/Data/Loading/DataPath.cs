using System.Collections.Generic;
using System.Linq;

namespace UIFramework.Data.Loading
{
    /// <summary>
    /// Where a value lives in the data assets, for messages: <c>Menus["Owner/demo"].Children[2](#form).Children[0].Width</c>.
    /// Immutable; every step returns a new path.
    /// </summary>
    internal sealed class DataPath
    {
        private readonly string text;

        private DataPath(string text)
        {
            this.text = text;
        }

        /// <summary>An entry of a data asset (<c>Menus["key"]</c>).</summary>
        internal static DataPath Entry(string asset, string key) => new($"{asset}[\"{key}\"]");

        /// <summary>A member of this object.</summary>
        internal DataPath Field(string name) => new(text + "." + name);

        /// <summary>An item of this list, with its id when it has one.</summary>
        internal DataPath Index(int index, string? id) => new(text + "[" + index + "]" + (string.IsNullOrEmpty(id) ? string.Empty : "(#" + id + ")"));

        public override string ToString() => text;
    }

    /// <summary>How bad a data message is.</summary>
    internal enum DataSeverity
    {
        /// <summary>Worth knowing (shown by <c>ui_validate</c>, traced at load).</summary>
        Info,

        /// <summary>Something is ignored or guessed; the entry still builds.</summary>
        Warning,

        /// <summary>The value / element / entry is skipped.</summary>
        Error
    }

    /// <summary>One validation or build message, qualified by the path of the offending value.</summary>
    internal readonly record struct DataMessage(DataSeverity Severity, string Path, string Text)
    {
        public override string ToString() => $"{Severity.ToString().ToUpperInvariant()} {Path}: {Text}";
    }

    /// <summary>The messages of one entry (validation and build). Adding never throws, so one bad value never stops the rest.</summary>
    internal sealed class DataMessageLog
    {
        private readonly List<DataMessage> items = new();
        private readonly HashSet<DataMessage> seen = new();

        internal IReadOnlyList<DataMessage> Items => items;

        internal int Count(DataSeverity severity) => items.Count(m => m.Severity == severity);

        /// <summary>Add a message; an identical one (a row template built again for another row) is only kept once.</summary>
        internal void Add(DataSeverity severity, DataPath path, string text)
        {
            var message = new DataMessage(severity, path.ToString(), text);
            if (seen.Add(message))
            {
                items.Add(message);
            }
        }

        internal void Error(DataPath path, string text) => Add(DataSeverity.Error, path, text);

        internal void Warn(DataPath path, string text) => Add(DataSeverity.Warning, path, text);

        internal void Info(DataPath path, string text) => Add(DataSeverity.Info, path, text);

        internal void AddRange(DataMessageLog other)
        {
            foreach (DataMessage message in other.items)
            {
                if (seen.Add(message))
                {
                    items.Add(message);
                }
            }
        }

        internal void Clear()
        {
            items.Clear();
            seen.Clear();
        }
    }
}
