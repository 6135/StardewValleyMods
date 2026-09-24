using System;
using System.Collections.Generic;

namespace UIFramework.Core
{
    /// <summary>
    /// One <see cref="ConsumerContext"/> per owner mod id (ordinal). The C# API (<see cref="ModEntry.GetApi"/>) and the
    /// data layer both go through here, so the C# half and the data half of a hybrid mod share the tooltip delay,
    /// default style, signal bindings and muted callbacks. A content pack id works as an owner too: contexts only
    /// need a string.
    /// </summary>
    internal sealed class ConsumerContexts
    {
        private readonly Dictionary<string, ConsumerContext> byId = new(StringComparer.Ordinal);

        /// <summary>The context of <paramref name="modId"/>, created on first use.</summary>
        internal ConsumerContext For(string modId)
        {
            modId ??= string.Empty;
            if (!byId.TryGetValue(modId, out ConsumerContext? context))
            {
                byId[modId] = context = new ConsumerContext(modId);
            }

            return context;
        }

        /// <summary>The context of <paramref name="modId"/> if one was created, else null.</summary>
        internal ConsumerContext? Find(string modId) => byId.TryGetValue(modId ?? string.Empty, out ConsumerContext? context) ? context : null;

        /// <summary>Every context created so far.</summary>
        internal IEnumerable<ConsumerContext> All => byId.Values;
    }
}
