using System;
using System.Collections.Specialized;
using System.Runtime.Caching;

namespace Ruzzie.Caching
{

    /// <summary>
    /// A fixed size cache that uses <see cref="MemoryCache"/> as a backing cache.
    /// </summary>
    /// <typeparam name="TValue">The value to cache</typeparam>
    public class MemoryCacheWithSizeLimit<TValue> : IFixedSizeCache<string,TValue>, IDisposable
    {
        private MemoryCache _cache;

        private readonly TimeSpan _slidingExpiration;


        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheWithSizeLimit{TValue}"/> class.
        /// </summary>
        /// <param name="maxCacheSizeInMb">The maximum cache size in mb.</param>
        public MemoryCacheWithSizeLimit(int maxCacheSizeInMb) : this(maxCacheSizeInMb, 300)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MemoryCacheWithSizeLimit{TValue}"/> class.
        /// </summary>
        /// <param name="maxCacheSizeInMb">The maximum cache size in mb.</param>
        /// <param name="defaultCacheDurationInSeconds">The default cache duration in seconds.</param>
        /// <exception cref="System.ArgumentException">Size cannot be less than one.</exception>
        public MemoryCacheWithSizeLimit(int maxCacheSizeInMb = 16, int defaultCacheDurationInSeconds = 90)
        {
            if (maxCacheSizeInMb < 1)
            {
                throw new ArgumentException("Size cannot be less than one.", nameof(maxCacheSizeInMb));
            }

            string cacheName = GetType().FullName;
            _cache = new MemoryCache(cacheName, new NameValueCollection
            {
                {"CacheMemoryLimitMegabytes", maxCacheSizeInMb.ToString()},
                {"PollingInterval","00:00:30" }
            });
            SizeInMb = maxCacheSizeInMb;
            _slidingExpiration = new TimeSpan(0, 0, defaultCacheDurationInSeconds, 0);                      
        }

        /// <summary>
        /// Gets the cache item count.
        /// </summary>
        /// <value>
        /// The cache item count.
        /// </value>
        public int CacheItemCount
        {
            get { return Convert.ToInt32(_cache.GetCount()); }
        }

        /// <summary>
        /// Gets the size in mb.
        /// </summary>
        /// <value>
        /// The size in mb.
        /// </value>
        // ReSharper disable once UnusedAutoPropertyAccessor.Global
        public int SizeInMb { get; }


        /// <summary>
        /// Get or add an item to the cache.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="addMethodWhenKeyNotFoundAction">The add method when key not found action.</param>
        /// <returns>The value of the item in cache if found, else the value provided by the add method.</returns>
        /// <exception cref="System.ArgumentNullException">When the key is null.</exception>
        public TValue GetOrAdd(string key, Func<string, TValue> addMethodWhenKeyNotFoundAction)
        {

            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            object valueFromCache = _cache.Get(key);
            if (valueFromCache == null)
            {
                TValue valueToStoreInCache = addMethodWhenKeyNotFoundAction.Invoke(key);
                if (valueToStoreInCache != null)
                {
                    _cache.Set(key, valueToStoreInCache, new CacheItemPolicy { SlidingExpiration = _slidingExpiration });                   
                }
                return valueToStoreInCache;
            }

            return (TValue)valueFromCache;           
        }

        /// <summary>
        /// Tries to get a value from the cache. If an item is in cache the value out parameter is set and true is returned.
        /// </summary>
        /// <param name="cacheKey">The cache key.</param>
        /// <param name="value">The value.</param>
        /// <returns>True then found otherwise false.</returns>
        public bool TryGet(string cacheKey, out TValue value)
        {
            var item = _cache.Get(cacheKey);
            if (item == null)
            {
                value = default(TValue);
                return false;
            }
            value = (TValue) item;

            return true;
        }


        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources.
        /// </summary>
        /// <param name="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                // free managed resources
                if (_cache != null)
                {
                    _cache.Dispose();
                    _cache = null;
                }
            }           
        }
    }
}