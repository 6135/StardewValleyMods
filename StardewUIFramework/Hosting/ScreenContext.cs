using System;
using UIFramework.Api;
using UIFramework.Core;

namespace UIFramework.Hosting
{
    /// <summary>
    /// The <see cref="IUIScreenContext"/> handed to one contributor for one menu: a thin view over the owner's
    /// <see cref="ScreenExposures"/> that remembers which mod is asking (so subscriptions are attributed and guarded
    /// per subscriber).
    /// </summary>
    internal sealed class ScreenContext : IUIScreenContext
    {
        private readonly ScreenExposures exposures;
        private readonly ConsumerContext contributor;

        internal ScreenContext(ScreenExposures exposures, ConsumerContext contributor)
        {
            this.exposures = exposures;
            this.contributor = contributor;
        }

        public string OwnerModId => exposures.Owner.ModId;
        public string MenuId => exposures.MenuId;
        public string[] Keys => exposures.Keys;

        public bool HasValue(string key) => exposures.HasValue(key ?? string.Empty);
        public string GetString(string key) => exposures.GetString(key ?? string.Empty);
        public double GetNumber(string key) => exposures.GetNumber(key ?? string.Empty);
        public bool GetBool(string key) => exposures.GetBool(key ?? string.Empty);
        public bool HasCommand(string command) => exposures.HasCommand(command ?? string.Empty);
        public void Invoke(string command) => exposures.Invoke(command ?? string.Empty);

        public void Subscribe(string eventName, Action handler)
        {
            if (!string.IsNullOrEmpty(eventName) && handler != null)
            {
                exposures.Subscribe(contributor, eventName, handler);
            }
        }

        public void Unsubscribe(string eventName, Action handler)
        {
            if (!string.IsNullOrEmpty(eventName) && handler != null)
            {
                exposures.Unsubscribe(contributor, eventName, handler);
            }
        }

        public override string ToString() => $"ScreenContext({OwnerModId}/{MenuId} for {contributor.ModId})";
    }
}
