using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace UIFramework.Core
{
    /// <summary>
    /// Per-menu timing of the last <see cref="FrameWindow"/> frames (<c>ui_perf</c>): layout, update, draw and the
    /// consumer callbacks raised inside them. Sampling is guarded by <see cref="Enabled"/> so it costs a single
    /// static bool check per phase when off. A frame ends when the menu finishes drawing.
    /// </summary>
    internal static class PerfCounters
    {
        /// <summary>Which timed section a sample belongs to.</summary>
        internal enum Phase
        {
            Layout,
            Update,
            Draw,
            Callback
        }

        /// <summary>Number of frames kept per menu.</summary>
        internal const int FrameWindow = 60;

        private static readonly ConditionalWeakTable<UIMenu, MenuPerf> PerMenu = new();
        private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

        /// <summary>Menu whose phase is currently running; callback time is attributed to it.</summary>
        private static MenuPerf? current;

        /// <summary>Turn sampling on or off; changing it drops the previous samples.</summary>
        internal static bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                Reset();
            }
        }

        private static bool enabled;

        /// <summary>Drop every sample.</summary>
        internal static void Reset() => PerMenu.Clear();

        /// <summary>Start timing a phase of <paramref name="menu"/>; dispose the scope to record it. A no-op scope when sampling is off.</summary>
        internal static Scope Begin(UIMenu menu, Phase phase, bool endsFrame = false)
        {
            if (!enabled)
            {
                return default;
            }

            MenuPerf perf = PerMenu.GetValue(menu, _ => new MenuPerf());
            var scope = new Scope(perf, current, phase, endsFrame, Stopwatch.GetTimestamp());
            current = perf;
            return scope;
        }

        /// <summary>Timestamp for a consumer callback about to run (0 when sampling is off).</summary>
        internal static long StartCallback() => enabled ? Stopwatch.GetTimestamp() : 0;

        /// <summary>Attribute the callback time since <paramref name="started"/> to the menu whose phase is running.</summary>
        internal static void EndCallback(long started)
        {
            if (started == 0 || current == null)
            {
                return;
            }

            current.Add(Phase.Callback, (Stopwatch.GetTimestamp() - started) * TicksToMs);
        }

        /// <summary>Human-readable summary of the samples for <paramref name="menu"/>, or null when there are none.</summary>
        internal static string? Describe(UIMenu menu)
        {
            return PerMenu.TryGetValue(menu, out MenuPerf? perf) && perf.FrameCount > 0 ? perf.Describe() : null;
        }

        /// <summary>A running phase; disposing records the elapsed time.</summary>
        internal readonly struct Scope : IDisposable
        {
            private readonly MenuPerf? perf;
            private readonly MenuPerf? previous;
            private readonly Phase phase;
            private readonly bool endsFrame;
            private readonly long started;

            internal Scope(MenuPerf perf, MenuPerf? previous, Phase phase, bool endsFrame, long started)
            {
                this.perf = perf;
                this.previous = previous;
                this.phase = phase;
                this.endsFrame = endsFrame;
                this.started = started;
            }

            public void Dispose()
            {
                if (perf == null)
                {
                    return;
                }

                perf.Add(phase, (Stopwatch.GetTimestamp() - started) * TicksToMs);
                if (endsFrame)
                {
                    perf.EndFrame();
                }

                current = previous;
            }
        }

        /// <summary>Ring buffer of per-frame phase totals for one menu.</summary>
        internal sealed class MenuPerf
        {
            private static readonly int PhaseCount = Enum.GetValues<Phase>().Length;

            private readonly double[,] frames = new double[FrameWindow, PhaseCount];
            private int index;

            /// <summary>Completed frames in the window (at most <see cref="FrameWindow"/>).</summary>
            internal int FrameCount { get; private set; }

            internal void Add(Phase phase, double ms) => frames[index, (int)phase] += ms;

            /// <summary>Close the current frame and start accumulating into the next slot.</summary>
            internal void EndFrame()
            {
                index = (index + 1) % FrameWindow;
                FrameCount = Math.Min(FrameCount + 1, FrameWindow);
                for (int p = 0; p < PhaseCount; p++)
                {
                    frames[index, p] = 0;
                }
            }

            /// <summary>Average / max milliseconds per phase over the completed frames.</summary>
            internal (double average, double max) Stats(Phase phase)
            {
                double total = 0, max = 0;
                for (int i = 1; i <= FrameCount; i++)
                {
                    double v = frames[(index - i + FrameWindow) % FrameWindow, (int)phase];
                    total += v;
                    max = Math.Max(max, v);
                }
                return (FrameCount == 0 ? 0 : total / FrameCount, max);
            }

            internal string Describe()
            {
                var sb = new StringBuilder();
                sb.Append(FrameCount).Append(" frame(s) sampled");
                foreach (Phase phase in Enum.GetValues<Phase>())
                {
                    (double average, double max) = Stats(phase);
                    sb.Append('\n').Append("    ").Append(phase.ToString().ToLowerInvariant().PadRight(9))
                      .Append("avg ").Append(average.ToString("0.000", CultureInfo.InvariantCulture)).Append(" ms   ")
                      .Append("max ").Append(max.ToString("0.000", CultureInfo.InvariantCulture)).Append(" ms");
                }
                sb.Append("\n    (callback time is also included in the phase that raised it)");
                return sb.ToString();
            }
        }
    }
}
