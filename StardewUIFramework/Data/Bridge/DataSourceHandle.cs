using System;
using System.Collections.Generic;
using StardewValley;
using UIFramework.Api;
using UIFramework.Core;
using UIFramework.Data.Building;
using UIFramework.Data.Expressions;
using UIFramework.Data.State;

namespace UIFramework.Data.Bridge
{
    /// <summary>
    /// A row source computed by C# (<see cref="IStardewUIApi.DefineDataSource"/>): data reads it as
    /// <c>"Source": "hook:ModId/name"</c>. Rows are lazy <see cref="SourceRow"/> values: <c>row.&lt;field&gt;</c> calls
    /// <see cref="Text"/> (numeric text becomes a number) and falls back to <see cref="Number"/>; <c>row.item</c> calls
    /// <see cref="Item"/>. Every delegate runs under the owner's callback guard. <see cref="Refresh"/> bumps the
    /// version (collections re-read the rows) and the state epoch (cells re-evaluate).
    /// </summary>
    internal sealed class DataSourceHandle : IUIDataSource
    {
        private readonly ConsumerContext owner;

        internal DataSourceHandle(ConsumerContext owner, string name)
        {
            this.owner = owner;
            Name = name;
        }

        public string Name { get; }

        public Func<int> Count { get; set; } = null!;

        public Func<int, string, string> Text { get; set; } = null!;

        public Func<int, string, double> Number { get; set; } = null!;

        public Func<int, Item> Item { get; set; } = null!;

        /// <summary>Incremented by <see cref="Refresh"/>.</summary>
        internal long Version { get; private set; }

        public void Refresh()
        {
            Version++;
            DataStateStore.Active?.BumpGlobal();
        }

        private string GuardId => "hook:" + Name;

        /// <summary>The current rows (one lazy value per index).</summary>
        internal IReadOnlyList<DataValue> Rows()
        {
            Func<int>? count = Count;
            int total = count == null ? 0 : owner.Invoke(GuardId, "Count", count, 0);
            total = Math.Clamp(total, 0, SourceBinding.MaxRows);
            var rows = new DataValue[total];
            for (int i = 0; i < total; i++)
            {
                rows[i] = DataValue.Opaque(new SourceRow(this, i, Version));
            }

            return rows;
        }

        /// <summary>Field <paramref name="field"/> of row <paramref name="index"/>; false when neither delegate knows it.</summary>
        internal bool TryGetField(int index, string field, out DataValue value)
        {
            if (field.Equals("item", StringComparison.OrdinalIgnoreCase) && Item is { } item)
            {
                value = DataValue.Opaque(owner.Invoke<Item?>(GuardId, "Item", () => item(index), null));
                return true;
            }

            if (field.Equals("index", StringComparison.OrdinalIgnoreCase))
            {
                value = DataValue.FromNumber(index);
                return true;
            }

            if (Text is { } text)
            {
                string? result = owner.Invoke<string?>(GuardId, "Text", () => text(index, field), null);
                if (result != null)
                {
                    value = StateAddress.Infer(result);
                    return true;
                }
            }

            if (Number is { } number)
            {
                value = DataValue.FromNumber(owner.Invoke(GuardId, "Number", () => number(index, field), 0d));
                return true;
            }

            value = DataValue.Null;
            return false;
        }

        /// <summary>One row of a C# source; equal rows (same source, index and version) keep collections from rebuilding.</summary>
        internal sealed class SourceRow
        {
            internal SourceRow(DataSourceHandle source, int index, long version)
            {
                Source = source;
                Index = index;
                Version = version;
            }

            internal DataSourceHandle Source { get; }
            internal int Index { get; }
            internal long Version { get; }

            public override bool Equals(object? obj) => obj is SourceRow other && other.Source == Source && other.Index == Index && other.Version == Version;

            public override int GetHashCode() => HashCode.Combine(Source, Index, Version);

            public override string ToString() => Source.TryGetField(Index, "name", out DataValue name) ? name.AsString() : Index.ToString();
        }
    }
}
