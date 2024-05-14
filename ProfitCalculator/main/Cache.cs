using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProfitCalculator.main
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
        ///
        /// </summary>
        public Cache(Func<T> buildCache)
        {
            this.buildCache = buildCache;
            isCacheValid = true;
            cache = this.buildCache();
        }

        /// <summary>
        /// Gets the cache. If the cache is invalid, it will be rebuilt.
        /// </summary>
        /// <returns></returns>
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
            cache = this.buildCache();
        }

        /// <summary>
        /// Checks if the cache is valid.
        /// </summary>
        /// <returns></returns>
        public bool IsCacheValid()
        {
            return isCacheValid;
        }

        /// <summary>
        /// Sets the build cache function.
        /// </summary>
        /// <param name="buildCache"></param>
        public void SetBuildCache(Func<T> buildCache)
        {
            this.buildCache = buildCache;
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