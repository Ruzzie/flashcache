using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Ruzzie.Common.Numerics;
using Volatile = Ruzzie.Common.Threading.Volatile;

namespace Ruzzie.Caching
{
    /// <summary>
    ///     Fixed size high performant in memory cache.
    ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
    ///     on the hashcode.
    ///     This cache guarantees a fixed size and read and write thread safety.
    ///     This variant uses pre-allocated entries, so GC is reduced. This comes at the cost of a tiny overhead when a hash collision occurs.
    /// </summary>
    /// <typeparam name="TKey">The cache key</typeparam>
    /// <typeparam name="TValue">The value to cache.</typeparam>
    public class FlashCacheWithPool<TKey, TValue> : IFixedSizeCache<TKey, TValue>
    {
        private readonly IEqualityComparer<TKey> _comparer;

        private readonly FlashEntryAlt[] _entries;
        private readonly int _maxItemCount;
        private readonly int _indexMask;
        //private readonly ConcurrentCircularOverwriteBuffer<FlashEntryAlt> _objectBufferPool;
        private readonly ConcurrentQueue<FlashEntryAlt> _objectBufferPool;

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety.
        /// </summary>
        /// <param name="maxItemCount">The (fixed) number of items this cache can hold.</param>
        /// <param name="comparer">The comparer to use for the keys.</param>
        /// <exception cref="ArgumentException">When the maxItemCount is less than 2.</exception>
        /// <remarks>
        ///     All lookups in the cache are an O(1)
        ///     operation.
        ///     The maximum size of the Cache object itself is guaranteed.
        /// </remarks>
        public FlashCacheWithPool(IEqualityComparer<TKey> comparer, int maxItemCount)
        {
            if (maxItemCount < 1)
            {
                throw new ArgumentException("Cannot be less than 2.", nameof(maxItemCount));
            }

            _maxItemCount = maxItemCount.FindNearestPowerOfTwoEqualOrLessThan();
            _indexMask = _maxItemCount - 1;

            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries = new FlashEntryAlt[_maxItemCount];

            for (int i = 0; i < _maxItemCount; i++)
            {
                _entries[i] = new FlashEntryAlt();
            }

            int objectBufferPoolSize = Environment.ProcessorCount * 8;
            _objectBufferPool = new ConcurrentQueue<FlashEntryAlt>(); //new ConcurrentCircularOverwriteBuffer<FlashEntryAlt>(objectBufferPoolSize);

            //fill the object pool buffer
            for (int i = 0; i < objectBufferPoolSize; i++)
            {
                //_objectBufferPool.WriteNext(new FlashEntryAlt());
                _objectBufferPool.Enqueue(new FlashEntryAlt());
            }
        }

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety. The cache will estimate the probable size of each type in the cache.
        ///     The size calculation in general use cases is pessimistic. If you see a big difference in real memory usage and the size of the cache, tune it with the parameters or give a larger size.
        /// </summary>
        /// <exception cref="ArgumentException">When the maximumSizeInMb is less than 1.</exception>
        /// <param name="maxItemCount">The (fixed) number of items this cache can hold.</param>
        /// <remarks>
        ///     All lookups in the cache are an O(1)
        ///     operation.
        ///     The maximum size of the Cache object itself is guaranteed.
        /// </remarks>
        public FlashCacheWithPool(int maxItemCount) : this(EqualityComparer<TKey>.Default, maxItemCount)
        {

        }

        /// <summary>
        ///     The actual size of the FlashCache internal array.
        /// </summary>
        public int MaxItemCount
        {
            get { return _maxItemCount; }
        }

        /// <summary>
        ///     Get an item for the given key. Or add them using the given value factory.
        /// </summary>
        /// <param name="key">The key to store the value for. Key can be null or default.</param>
        /// <param name="valueFactory">
        ///     The function that generated the value to store. This will only be executed when the key is
        ///     not yet present.
        /// </param>
        /// <returns>
        ///     The value. If it was cached the cached value is returned. If it was not cached the value from the value
        ///     factory is returned.
        /// </returns>
        /// <exception cref="Exception">A delegate callback throws an exception.</exception>
        public TValue GetOrAdd(in TKey key, in Func<TKey, TValue> valueFactory)
        {
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }

            int hashCode = GetHashcodeForKey(key);
            int index = GetTargetEntryIndexForHashcode(hashCode);

            FlashEntryAlt entry = GetFlashEntryWithMemoryBarrier(index);

            //[The item is in the cache, return the value]
            var entryHasValue = entry.HasValue;
            var entryKey = entry.Key;
            var entryHashCode = entry.HashCode;
            var entryValue = entry.Value;

            if (entryHasValue && KeyIsEqual(key, hashCode, entryKey, entryHashCode))
            {
                return entryValue;
            }
            
            //2. Check if there are objects available from the pool, so we don't have to allocate a new object
            //if (!_objectBufferPool.ReadNext(out var newEntry))
            if (!_objectBufferPool.TryDequeue(out var newEntry))
            {
                //If this happens the objectBufferPool is too small for the number of simultaneous requests
                if (_objectBufferPool.Count == 0)
                {
                    //TODO: Resize Pool?
                    //_objectBufferPool.WriteNext(new FlashEntryAlt());
                    _objectBufferPool.Enqueue(new FlashEntryAlt());
                }

                newEntry = new FlashEntryAlt();
            }

            //3. Get the value for the new entry
            TValue newValue = valueFactory.Invoke(key);

            //4. Store the new value in the cache
            var oldEntry = Interlocked.Exchange(ref _entries[index], newEntry.Set(hashCode, key, newValue));

            // reset old entry and return it to the pool
            //_objectBufferPool.WriteNext( entry.Reset());
            _objectBufferPool.Enqueue(oldEntry.Reset());

#if NET40 || PORTABLE
            System.Threading.Thread.MemoryBarrier();
#else
            Interlocked.MemoryBarrier();
#endif
            return newValue;
        }

        /// <summary>
        ///     Returns the current items in cache. Beware this is an O(n) operation.
        /// </summary>
        public int CacheItemCount
        {
            get
            {
                var itemCount = 0;
                for (var i = 0; i < _entries.Length; i++)
                {
                    FlashEntryAlt flashEntry = Volatile.Read(ref _entries[i]);
                    if (flashEntry.HasValue)
                    {
                        itemCount++;
                    }
                }
                return itemCount;
            }
        }

        /// <summary>
        ///     Gets the value associated with the specified key.
        /// </summary>
        /// <param name="cacheKey">The key of the value to get.</param>
        /// <param name="value">
        ///     When this method returns, contains the value associated with the specified key, if the key is
        ///     found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.
        /// </param>
        /// <returns>
        ///     true if the <see cref="FlashCache{TKey,TValue}" /> contains an element with the specified key; otherwise,
        ///     false.
        /// </returns>
        /// <remarks>
        ///     If the key is not found, then the value parameter gets the appropriate default value for the type TValue; for
        ///     example, 0 (zero) for integer types, false for Boolean types, and null for reference types.
        /// </remarks>
        public bool TryGet(in TKey cacheKey, out TValue value)
        {
            value = default!;

            int hashCode = GetHashcodeForKey(cacheKey);
            int index = GetTargetEntryIndexForHashcode(hashCode);

            FlashEntryAlt entry = GetFlashEntryWithMemoryBarrier(index);

            if (!entry.HasValue)
            {
                return false;
            }

            if (!KeyIsEqual(cacheKey, hashCode, entry.Key, entry.HashCode))
            {
                return false;
            }

            value = entry.Value;
            return true;
        }

        /// <summary>
        ///     Trims the cache. This method always returns 0 for <see cref="FlashCache{TKey,TValue}" />. Since it has a guaranteed
        ///     fixed size and needs no trimming.
        /// </summary>
        /// <param name="trimOptions">The trim options.</param>
        /// <returns>0</returns>
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "trimOptions", Justification = "Needed for interface implementation.")]
        public int Trim(in TrimOptions trimOptions)
        {//TODO: Extract to trimmablecache interface for flashcachewithbuckets
            return 0; //no trim necessary with this implementation.
        }

#if HAVE_METHODINLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private FlashEntryAlt GetFlashEntryWithMemoryBarrier(int targetEntry)
        {
            return Volatile.Read(ref _entries[targetEntry]);
        }

#if HAVE_METHODINLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private bool KeyIsEqual(in TKey key, int hashCode, TKey entryKey, int entryHashCode)
        {
            return entryHashCode == hashCode && _comparer.Equals(key, entryKey);
        }

#if HAVE_METHODINLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private int GetTargetEntryIndexForHashcode(int hashCode)
        {
            return (hashCode) & (_indexMask); // bitwise % operator since array is always length power of 2
        }

#if HAVE_METHODINLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private int GetHashcodeForKey(in TKey key)
        {
            return _comparer.GetHashCode(key);
        }

        internal class FlashEntryAlt
        {
            private long _hasValueFlag;
            private volatile int _hashCode;

            public bool HasValue
            {
                get
                {
                    return Interlocked.Read(ref _hasValueFlag) == 1;
                }
                private set
                {
                    Interlocked.Exchange(ref _hasValueFlag, value ? 1 : 0);
                }
            }

            public int HashCode
            {
                get => _hashCode;
                private set => _hashCode = value;
            }

            public TKey Key { get; private set; } = default!;

            public TValue Value { get; private set; } = default!;

            public FlashEntryAlt()
            {
                _hasValueFlag = 0;
            }

#if HAVE_METHODINLINING
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            public FlashEntryAlt Set(int hashCode, TKey key, TValue value)
            {
                HashCode = hashCode;
                Key = key;
                Value = value;
                HasValue = true;
                return this;
            }

#if HAVE_METHODINLINING
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
            public FlashEntryAlt Reset()
            {
                HasValue = false;
                HashCode = 0;
                Key      = default!;
                Value    = default!;
                return this;
            }
        }
    }
}