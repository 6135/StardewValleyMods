using System;

namespace UIFramework.Core
{
    /// <summary>Numeric helpers shared by the value components.</summary>
    internal static class Numbers
    {
        /// <summary>Tolerance under which two UI values count as the same (so floating-point noise never re-raises change events).</summary>
        private const double Epsilon = 1e-9;

        /// <summary>True when <paramref name="a"/> and <paramref name="b"/> are equal within <see cref="Epsilon"/>.</summary>
        internal static bool Same(double a, double b) => Math.Abs(a - b) < Epsilon;
    }
}
