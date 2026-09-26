using System;
using System.Collections.Generic;
using StardewModdingAPI;
using UIFramework.Core;

namespace UIFramework.Data
{
    /// <summary>
    /// An ordered list of refreshers that is itself a refresher. Structural elements (<c>If</c>, <c>Switch</c> pages)
    /// give their subtree its own group, refreshed only while the subtree is built and dropped with it. A faulting
    /// refresher is logged once and skipped afterwards.
    /// </summary>
    internal sealed class RefresherGroup : IDataRefresher
    {
        private readonly List<IDataRefresher> items = new();
        private readonly HashSet<IDataRefresher> faulted = new();
        private readonly string owner;
        private readonly string id;

        internal RefresherGroup(string owner, string id)
        {
            this.owner = owner;
            this.id = id;
        }

        /// <summary>Number of refreshers (diagnostics).</summary>
        internal int Count => items.Count;

        internal void Add(IDataRefresher refresher) => items.Add(refresher);

        internal void Clear()
        {
            items.Clear();
            faulted.Clear();
        }

        public void Refresh(bool opening)
        {
            // index loop: refreshing an If can add refreshers to a child group, never to this one
            for (int i = 0; i < items.Count; i++)
            {
                IDataRefresher refresher = items[i];
                if (faulted.Contains(refresher))
                {
                    continue;
                }

                try
                {
                    refresher.Refresh(opening);
                }
                catch (Exception ex)
                {
                    faulted.Add(refresher);
                    UIServices.Log($"[{owner}] a data value of '{id}' failed to refresh and is no longer updated: {ex.Message}", LogLevel.Error);
                }
            }
        }
    }
}
