using System;
using System.Collections.Generic;
using System.Globalization;
using StardewModdingAPI;
using UIFramework.Api;

namespace UIFramework.Core
{
    /// <summary>
    /// Per-thread bookkeeping for the signal graph: the dependency-tracking stack (which computed is currently
    /// evaluating, so every signal it reads registers itself) and the notification batch (subscribers run once,
    /// after the write that changed a signal has completed, not in the middle of it).
    /// </summary>
    internal static class SignalScheduler
    {
        /// <summary>Upper bound on notifications per batch, so a handler that keeps changing what it listens to cannot hang the game.</summary>
        private const int MaxNotificationsPerBatch = 10000;

        [ThreadStatic] private static Stack<HashSet<Reactive>>? tracking;
        [ThreadStatic] private static Stack<Computed>? evaluating;
        [ThreadStatic] private static List<Reactive>? pending;
        [ThreadStatic] private static HashSet<Reactive>? pendingSet;
        [ThreadStatic] private static int batchDepth;

        // ---------------------------------------------------------------------------------------------------------
        //  Dependency tracking
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Record <paramref name="source"/> as a dependency of the computed being evaluated (if any).</summary>
        internal static void Track(Reactive source)
        {
            if (tracking != null && tracking.Count > 0)
            {
                tracking.Peek().Add(source);
            }
        }

        /// <summary>True while <paramref name="computed"/> is somewhere on the evaluation stack (reading it again would be a cycle).</summary>
        internal static bool IsEvaluating(Computed computed) => evaluating != null && evaluating.Contains(computed);

        /// <summary>Start collecting the dependencies of <paramref name="computed"/>.</summary>
        internal static void BeginEvaluation(Computed computed)
        {
            (tracking ??= new Stack<HashSet<Reactive>>()).Push(new HashSet<Reactive>());
            (evaluating ??= new Stack<Computed>()).Push(computed);
        }

        /// <summary>Stop collecting and return what <paramref name="computed"/> read.</summary>
        internal static HashSet<Reactive> EndEvaluation(Computed computed)
        {
            if (evaluating != null && evaluating.Count > 0 && evaluating.Peek() == computed)
            {
                evaluating.Pop();
            }

            return tracking != null && tracking.Count > 0 ? tracking.Pop() : new HashSet<Reactive>();
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Notification batches
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Open a batch: notifications queued until the matching <see cref="EndBatch"/> run then, in order, once each.</summary>
        internal static void BeginBatch() => batchDepth++;

        /// <summary>Close a batch; the outermost close flushes the queued notifications.</summary>
        internal static void EndBatch()
        {
            if (--batchDepth > 0)
            {
                return;
            }

            batchDepth = 0;
            Flush();
        }

        /// <summary>Queue <paramref name="source"/> for notification (no-op if it is already queued and not yet notified).</summary>
        internal static void Enqueue(Reactive source)
        {
            pending ??= new List<Reactive>();
            pendingSet ??= new HashSet<Reactive>();
            if (pendingSet.Add(source))
            {
                pending.Add(source);
            }
        }

        /// <summary>Notify every queued reactive. Handlers may change signals; those changes are appended to the same batch.</summary>
        private static void Flush()
        {
            if (pending == null || pending.Count == 0)
            {
                return;
            }

            batchDepth++;
            try
            {
                for (int i = 0; i < pending.Count; i++)
                {
                    if (i >= MaxNotificationsPerBatch)
                    {
                        UIServices.Log("Signal notification batch exceeded " + MaxNotificationsPerBatch + " notifications; a subscriber keeps changing the signals it listens to. Remaining notifications were dropped.", LogLevel.Warn);
                        break;
                    }

                    Reactive source = pending[i];
                    pendingSet!.Remove(source);
                    source.NotifySubscribers();
                }
            }
            finally
            {
                pending.Clear();
                pendingSet!.Clear();
                batchDepth--;
            }
        }
    }

    /// <summary>
    /// Common part of <see cref="Signal"/> and <see cref="Computed"/>: the version counter, the computeds that depend
    /// on this value (invalidated synchronously when it changes) and the subscribers (notified in batches).
    /// </summary>
    internal abstract class Reactive
    {
        private static int nextId;

        private readonly List<Subscription> subscriptions = new();
        private readonly HashSet<Computed> dependents = new();

        protected Reactive(ConsumerContext consumer, string kind)
        {
            Consumer = consumer;
            Id = kind + "#" + (++nextId).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The consumer that created the value (its callback guard wraps every handler).</summary>
        internal ConsumerContext Consumer { get; }

        /// <summary>Identifier used in log lines and mute keys.</summary>
        internal string Id { get; }

        /// <summary>Incremented on every change (signal) or invalidation (computed).</summary>
        public int Version { get; private set; }

        /// <summary>The value right now (recomputed first when stale); reading it registers a dependency like the typed getters do.</summary>
        internal abstract ReactiveValue Current { get; }

        /// <summary>Register the currently evaluating computed as a dependent (call from every getter).</summary>
        protected void Track() => SignalScheduler.Track(this);

        internal void AddDependent(Computed computed) => dependents.Add(computed);

        internal void RemoveDependent(Computed computed) => dependents.Remove(computed);

        /// <summary>Bump the version, invalidate every dependent computed and queue this value for notification.</summary>
        protected void Changed()
        {
            Version++;
            foreach (Computed dependent in new List<Computed>(dependents))
            {
                dependent.Invalidate();
            }

            SignalScheduler.Enqueue(this);
        }

        // ---------------------------------------------------------------------------------------------------------
        //  Subscribers
        // ---------------------------------------------------------------------------------------------------------

        /// <summary>Consumer subscription: the handler runs through the callback guard.</summary>
        public void Subscribe(Action handler)
        {
            ArgumentNullException.ThrowIfNull(handler);

            subscriptions.Add(new Subscription(handler, () => Consumer.Invoke(Id, "Subscribe", handler)));
        }

        public void Unsubscribe(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            int index = subscriptions.FindIndex(s => s.Handler.Equals(handler));
            if (index >= 0)
            {
                subscriptions.RemoveAt(index);
            }
        }

        /// <summary>Framework subscription (bindings): the handler is trusted and runs unguarded.</summary>
        internal void SubscribeInternal(Action handler) => subscriptions.Add(new Subscription(handler, handler));

        internal void UnsubscribeInternal(Action handler) => Unsubscribe(handler);

        /// <summary>Run every subscriber (called by the scheduler when the batch flushes).</summary>
        internal void NotifySubscribers()
        {
            foreach (Subscription subscription in subscriptions.ToArray())
            {
                subscription.Invoke();
            }
        }

        private sealed class Subscription
        {
            internal Action Handler { get; }
            internal Action Invoke { get; }

            internal Subscription(Action handler, Action invoke)
            {
                Handler = handler;
                Invoke = invoke;
            }
        }
    }

    /// <summary>Which typed view of a reactive value was written last (its other views convert from it).</summary>
    internal enum ReactiveKind
    {
        Text,
        Number,
        Flag
    }

    /// <summary>A stored value with string / number / flag views and typed conversions between them.</summary>
    internal readonly struct ReactiveValue
    {
        internal string Text { get; }
        internal double Number { get; }
        internal bool Flag { get; }
        internal ReactiveKind Kind { get; }

        private ReactiveValue(string text, double number, bool flag, ReactiveKind kind)
        {
            Text = text;
            Number = number;
            Flag = flag;
            Kind = kind;
        }

        internal static ReactiveValue FromText(string? text)
        {
            text ??= string.Empty;
            bool parsed = double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double number);
            bool flag = parsed ? !Numbers.Same(number, 0) : bool.TryParse(text, out bool b) && b;
            return new ReactiveValue(text, parsed ? number : 0, flag, ReactiveKind.Text);
        }

        internal static ReactiveValue FromNumber(double number)
        {
            return new ReactiveValue(number.ToString("R", CultureInfo.InvariantCulture), number, !Numbers.Same(number, 0), ReactiveKind.Number);
        }

        internal static ReactiveValue FromFlag(bool flag)
        {
            return new ReactiveValue(flag.ToString(), flag ? 1 : 0, flag, ReactiveKind.Flag);
        }

        /// <summary>True when this value equals <paramref name="other"/> as seen through <paramref name="other"/>'s own kind.</summary>
        internal bool SameAs(in ReactiveValue other)
        {
            return other.Kind switch
            {
                ReactiveKind.Number => Numbers.Same(Number, other.Number),
                ReactiveKind.Flag => Flag == other.Flag,
                _ => string.Equals(Text, other.Text, StringComparison.Ordinal)
            };
        }
    }

    /// <summary>A writable reactive value (see <see cref="IUISignal"/>).</summary>
    internal sealed class Signal : Reactive, IUISignal
    {
        private ReactiveValue value;

        internal Signal(ConsumerContext consumer, ReactiveValue initial) : base(consumer, "signal")
        {
            value = initial;
        }

        internal override ReactiveValue Current
        {
            get
            {
                Track();
                return value;
            }
        }

        public string Value
        {
            get => Current.Text;
            set => Set(ReactiveValue.FromText(value));
        }

        public double Number
        {
            get => Current.Number;
            set => Set(ReactiveValue.FromNumber(value));
        }

        public bool Flag
        {
            get => Current.Flag;
            set => Set(ReactiveValue.FromFlag(value));
        }

        /// <summary>Store a new value; dependents are invalidated at once, subscribers after the write completes.</summary>
        private void Set(ReactiveValue next)
        {
            if (value.SameAs(next))
            {
                return;
            }

            value = next;
            SignalScheduler.BeginBatch();
            try
            {
                Changed();
            }
            finally
            {
                SignalScheduler.EndBatch();
            }
        }

        public override string ToString() => $"Signal({Id} = '{value.Text}')";
    }

    /// <summary>A lazy, cached derived value with automatic dependency tracking (see <see cref="IUIComputed"/>).</summary>
    internal sealed class Computed : Reactive, IUIComputed
    {
        private readonly Func<ReactiveValue> compute;
        private readonly HashSet<Reactive> dependencies = new();
        private ReactiveValue value;
        private bool dirty = true;
        private bool cycleLogged;

        /// <summary><paramref name="compute"/> must already convert the consumer delegate's result into a <see cref="ReactiveValue"/>.</summary>
        internal Computed(ConsumerContext consumer, Func<ReactiveValue> compute, ReactiveValue fallback) : base(consumer, "computed")
        {
            this.compute = compute;
            value = fallback;
        }

        public string Value => Current.Text;

        public double Number => Current.Number;

        public bool Flag => Current.Flag;

        /// <summary>The cached value, recomputed first if a dependency changed since the last read.</summary>
        internal override ReactiveValue Current
        {
            get
            {
                Track();
                Ensure();
                return value;
            }
        }

        /// <summary>Mark stale: bump the version, propagate to dependents, queue a notification. Already-stale computeds stop the walk (this is what breaks cycles).</summary>
        internal void Invalidate()
        {
            if (dirty)
            {
                return;
            }

            dirty = true;
            Changed();
        }

        /// <summary>Recompute when stale, re-tracking the dependencies read during the evaluation.</summary>
        private void Ensure()
        {
            if (!dirty)
            {
                return;
            }

            if (SignalScheduler.IsEvaluating(this))
            {
                LogCycle();
                return;
            }

            SignalScheduler.BeginEvaluation(this);
            HashSet<Reactive> read;
            try
            {
                value = Consumer.Invoke(Id, "Compute", compute, value);
            }
            finally
            {
                read = SignalScheduler.EndEvaluation(this);
            }

            RetrackDependencies(read);
            dirty = false;
        }

        private void LogCycle()
        {
            if (cycleLogged)
            {
                return;
            }

            cycleLogged = true;
            UIServices.Log($"[{Consumer.ModId}] {Id} depends on itself (directly or through another computed); the stale value is kept.", LogLevel.Error);
        }

        /// <summary>Swap the dependency set for what the last evaluation actually read.</summary>
        private void RetrackDependencies(HashSet<Reactive> read)
        {
            read.Remove(this);
            foreach (Reactive old in dependencies)
            {
                if (!read.Contains(old))
                {
                    old.RemoveDependent(this);
                }
            }

            foreach (Reactive source in read)
            {
                source.AddDependent(this);
            }

            dependencies.Clear();
            dependencies.UnionWith(read);
        }

        public override string ToString() => $"Computed({Id}{(dirty ? ", stale" : string.Empty)})";
    }
}
