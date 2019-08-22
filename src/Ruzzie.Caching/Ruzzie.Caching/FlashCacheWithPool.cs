using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Ruzzie.Common.Collections;
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
        private readonly int _sizeInMb;
        private readonly int _maxItemCount;
        private readonly int _indexMask;
        private readonly ConcurrentCircularOverwriteBuffer<FlashEntryAlt> _objectBufferPool;

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety. The cache will estimate the probable size of each type in the cache. 
        ///     The size calculation in general use cases is pessimistic. If you see a big difference in real memory usage and the size of the cache, tune it with the parameters or give a larger size.
        /// </summary>
        /// <param name="maximumSizeInMb">
        ///     The maximum desired size in MegaBytes of the cache. The cache size will be an
        ///     approximation of the size in Mb's.
        /// </param>
        /// <param name="comparer">The desired equality comparer to use for comparing keys.</param>
        /// <param name="averageSizeInBytesOfKey">
        ///     Default -1. Only pass a value if the <typeparamref name="TKey" /> is a value
        ///     type. This parameter takes the given bytes for calculating the maximum size of the cache.
        /// </param>
        /// <param name="averageSizeInBytesOfValue">
        ///     Default -1. Only pass a value if the <typeparamref name="TValue" /> is a value
        ///     type. This parameter takes the given bytes for calculating the maximum size of the cache.
        /// </param>
        /// <exception cref="ArgumentException">When the maximumSizeInMb is less than 1.</exception>
        /// <remarks>
        ///     The size in Mb's is an estimation. If the key or value for the cache is a reference type, it does not take into
        ///     account the memory space the data of the reference type hold by default. All lookups in the cache are an O(1)
        ///     operation.
        ///     The maximum size of the Cache object itself is guaranteed.
        /// </remarks>        
        public FlashCacheWithPool(in int maximumSizeInMb, IEqualityComparer<TKey> comparer, in int averageSizeInBytesOfKey = -1,
            in int averageSizeInBytesOfValue = -1)
        {
            if (maximumSizeInMb < 1)
            {
                throw new ArgumentException("Cannot be less than one.", nameof(maximumSizeInMb));
            }

            int flashEntryTypeSize = CalculateFlashEntryTypeSize(averageSizeInBytesOfKey, averageSizeInBytesOfValue);

            _maxItemCount = SizeHelper.CalculateMaxItemCountInPowerOfTwo(maximumSizeInMb, flashEntryTypeSize);
            _indexMask = _maxItemCount - 1;

            _sizeInMb = ((_maxItemCount * flashEntryTypeSize) / 1024) / 1024;

            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries = new FlashEntryAlt[_maxItemCount];
            for (int i = 0; i < _maxItemCount; i++)
            {
                _entries[i] = new FlashEntryAlt();
            }

            int objectBufferPoolSize = Environment.ProcessorCount * 8;
            _objectBufferPool = new ConcurrentCircularOverwriteBuffer<FlashEntryAlt>(objectBufferPoolSize);
            
            //fill the buffer
            for (int i = 0; i < objectBufferPoolSize; i++)
            {
                _objectBufferPool.WriteNext(new FlashEntryAlt());
            }
        }

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety. The cache will estimate the probable size of each type in the cache. 
        ///     The size calculation in general use cases is pessimistic. If you see a big difference in real memory usage and the size of the cache, tune it with the parameters or give a larger size.
        /// </summary>
        /// <param name="maximumSizeInMb">
        ///     The maximum desired size in MegaBytes of the cache. The cache size will be an
        ///     approximation of the size in Mb's.
        /// </param>
        /// <param name="averageSizeInBytesOfKey">
        ///     Default -1. Only pass a value if the <typeparamref name="TKey" /> is a value
        ///     type. This parameter takes the given bytes for calculating the maximum size of the cache.
        /// </param>
        /// <param name="averageSizeInBytesOfValue">
        ///     Default -1. Only pass a value if the <typeparamref name="TValue" /> is a value
        ///     type. This parameter takes the given bytes for calculating the maximum size of the cache.
        /// </param>
        /// <exception cref="ArgumentException">When the maximumSizeInMb is less than 1.</exception>
        /// <remarks>
        ///     The size in Mb's is an estimation. If the key or value for the cache is a reference type, it does not take into
        ///     account the memory space the data of the reference type hold by default. All lookups in the cache are an O(1)
        ///     operation.
        ///     The maximum size of the Cache object itself is guaranteed.
        /// </remarks>   
        public FlashCacheWithPool(in int maximumSizeInMb,
            in int averageSizeInBytesOfKey = -1,
            in int averageSizeInBytesOfValue = -1) : this(maximumSizeInMb, EqualityComparer<TKey>.Default,
            averageSizeInBytesOfKey, averageSizeInBytesOfValue)
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

            if (entry.HasValue)
            {
                if (KeyIsEqual(key, entry, hashCode))
                {
                    return entry.Value;
                }
                else
                {
                    //Collision
                    if (!_objectBufferPool.ReadNext(out var newEntry))
                    {
                        //If this happens the objectBufferPool is too small for the number of simultaneous requests
                        newEntry = new FlashEntryAlt();
                        _objectBufferPool.WriteNext(new FlashEntryAlt());
                    }
                    
                    TValue newValue = valueFactory.Invoke(key);
                    Interlocked.Exchange(ref _entries[index], newEntry.Set(hashCode, key, newValue));
                    entry.Reset();
                    _objectBufferPool.WriteNext(entry);
                    return newValue;
                }
            }

            TValue value = valueFactory.Invoke(key);

#if NET40 || PORTABLE
            System.Threading.Thread.MemoryBarrier();
#else
            System.Threading.Interlocked.MemoryBarrier();
#endif 
            entry.Set(hashCode, key, value);
            return value;
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
                    if (flashEntry.HasValue /*ReferenceEquals(flashEntry, null)*/)
                    {
                        itemCount++;
                    }
                }
                return itemCount;
            }
        }

        /// <summary>
        ///     The calculated maximum size in MB's that this cache should be.
        /// </summary>
        public int SizeInMb
        {
            get { return _sizeInMb; }
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

            if (!entry.HasValue)//ReferenceEquals(entry, null))
            {
                return false;
            }

            if (!KeyIsEqual(cacheKey, entry, hashCode))
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

        internal static int CalculateFlashEntryTypeSize(in int averageSizeInBytesOfKey = -1, in int averageSizeInBytesOfValue = -1)
        {
            
            int entryTypeSize = TypeHelper.SizeOf(new FlashEntryAlt(0, default, default)) +
                                (averageSizeInBytesOfKey > 0 ? averageSizeInBytesOfKey : 0) +
                                (averageSizeInBytesOfValue > 0 ? averageSizeInBytesOfValue : 0);
            return entryTypeSize;
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
        private bool KeyIsEqual(in TKey key, FlashEntryAlt entry, int hashCode)
        {
            return entry.HashCode == hashCode && _comparer.Equals(key, entry.Key);
        }

#if HAVE_METHODINLINING
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private int GetTargetEntryIndexForHashcode(in int hashCode)
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
            public int HashCode { get; private set; }
            public TKey Key { get; private set; }
            public TValue Value { get; private set; }
            public bool HasValue { get; private set; }
            public FlashEntryAlt()
            {
                HasValue = false;
            }

            public FlashEntryAlt(int hashCode, TKey key, TValue value)
            {
               Set(hashCode, key, value);
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
            public void Reset()
            {
                HasValue = false;
                HashCode = 0;
                Key = default;
                Value = default;
            }

           
        }
    }
}