using System;

namespace CoreUtils.management.memory
{
    /// <summary>
    /// The cache class. This class is used to store data that is used by multiple classes. Cache can be invalidated and rebuilt.
    /// </summary>
    public class Cache<T>
    {
        private T cache;
        private bool isCacheValid;
        private Func<T> buildCache;

        /// <summary>
        /// Initializes a new instance of the Cache class.
        /// </summary>
        public Cache(Func<T> buildCache)
        {
            this.buildCache = buildCache ?? throw new ArgumentNullException(nameof(buildCache));
            isCacheValid = true;
            cache = this.buildCache();
        }

        /// <summary>
        /// Gets the cache. If the cache is invalid, it will be rebuilt.
        /// </summary>
        /// <returns>The cached value.</returns>
        public T GetCache()
        {
            if (!isCacheValid)
            {
                RebuildCache();
            }
            return cache;
        }

        /// <summary>
        /// Invalidates the cache. The cache will be rebuilt the next time it is accessed.
        /// </summary>
        public void InvalidateCache()
        {
            isCacheValid = false;
        }

        /// <summary>
        /// Rebuilds the cache.
        /// </summary>
        public void RebuildCache()
        {
            cache = buildCache();
            isCacheValid = true;
        }

        /// <summary>
        /// Checks if the cache is valid.
        /// </summary>
        /// <returns>True if the cache is valid; otherwise, false.</returns>
        public bool IsCacheValid()
        {
            return isCacheValid;
        }

        /// <summary>
        /// Sets the build cache function.
        /// </summary>
        /// <param name="buildCache">The function to build the cache.</param>
        public void SetBuildCache(Func<T> buildCache)
        {
            this.buildCache = buildCache ?? throw new ArgumentNullException(nameof(buildCache));
        }

        /// <summary>
        /// Clears the cache and invalidates it.
        /// </summary>
        public void ClearCache()
        {
            cache = default;
            InvalidateCache();
        }
    }
}