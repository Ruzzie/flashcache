using System;
//using System.CodeDom;
using System.Collections.Specialized;
#if DNXCORE50
using Microsoft.Framework.Caching.Memory;
#else
using System.Runtime.Caching;
#endif

namespace Ruzzie.Caching
{
    /// <summary>
    /// A fixed size cache that uses <see cref="MemoryCache"/> as a backing cache.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class MemoryCacheWithSizeLimit<TValue> : IMemoryCacheWithLimit<string, TValue>, IDisposable
    {
        private MemoryCache _cache;

        private readonly TimeSpan _slidingExpiration;

        public MemoryCacheWithSizeLimit(int maxCacheSizeInMb) : this(maxCacheSizeInMb, 300)
        {
        }

        public MemoryCacheWithSizeLimit(int maxCacheSizeInMb = 16, int defaultCacheDurationInSeconds = 90)
        {
            if (maxCacheSizeInMb < 1)
            {
                throw new ArgumentException("Size cannot be less than one.", nameof(maxCacheSizeInMb));
            }

            string cacheName = GetType().FullName;
#if DNXCORE50
           _cache = new MemoryCache(new MemoryCacheOptions()
            {
                CompactOnMemoryPressure = true,
                ExpirationScanFrequency = new TimeSpan(0, 0, 0, 30)
            });            
#else
             _cache = new MemoryCache(cacheName, new NameValueCollection
            {
                {"CacheMemoryLimitMegabytes", maxCacheSizeInMb.ToString()},
                {"PollingInterval","00:00:30" }
            });
#endif

            SizeInMb = maxCacheSizeInMb;
            _slidingExpiration = new TimeSpan(0, 0, defaultCacheDurationInSeconds, 0);
        }

        public int CacheItemCount
        {
            get
            {
#if DNXCORE50
                return _cache.Count;
#else
                return Convert.ToInt32(_cache.GetCount());
#endif
            }
        }

        public int SizeInMb { get; }


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
#if DNXCORE50
                    _cache.Set(key, valueToStoreInCache, new MemoryCacheEntryOptions {SlidingExpiration = _slidingExpiration});
#else
                    _cache.Set(key, valueToStoreInCache, new CacheItemPolicy { SlidingExpiration = _slidingExpiration });
#endif
                }
                return valueToStoreInCache;
            }

            return (TValue)valueFromCache;
        }

        public bool TryGet(string cacheKey, out TValue value)
        {
            var item = _cache.Get(cacheKey);
            if (item == null)
            {
                value = default(TValue);
                return false;
            }
            value = (TValue)item;

            return true;
        }


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

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