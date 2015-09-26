using System;
using System.Collections.Generic;
using System.Threading;

namespace Ruzzie.Caching
{
    /// <summary>
    /// Fixed size high performant in memory cache.
    ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
    ///     on the hashcode.
    ///     This cache guarantees a fixed size and read and write thread safety.
    /// </summary>
    /// <typeparam name="TKey">The cache key</typeparam>
    /// <typeparam name="TValue">The value to cache.</typeparam>
    public class FlashCache<TKey, TValue> : IFixedSizeCache<TKey, TValue>
    {
        private readonly IEqualityComparer<TKey> _comparer;

        private readonly FlashEntry[] _entries;
        private readonly int _sizeInMb;
        private readonly int _maxItemCount;

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety.
        /// </summary>
        /// <param name="maximumSizeInMb">The maximum desired size in MegaBytes of the cache. The cache size will be an approximation of the size in Mb's.</param>
        /// <param name="comparer">Optionally the desired equality comparer to use for comparing keys.</param>
        /// <param name="averageSizeInBytesOfKey">Default -1. Only pass a value if the <typeparamref name="TKey"/> is a value type. This parameter takes the given bytes for calculating the maximum size of the cache.</param>
        /// <param name="averageSizeInBytesOfValue">Default -1. Only pass a value if the <typeparamref name="TValue"/> is a value type. This parameter takes the given bytes for calculating the maximum size of the cache.</param>
        /// <remarks>The size in Mb's is an estimation. If the key or value for the cache is a reference type, it does not take into account the memory space the data of the reference type hold by default. All lookups in the cache are an O(1) operation.
        /// The maximum size of the Cache object itself is guaranteed.
        /// </remarks>
        public FlashCache(int maximumSizeInMb, IEqualityComparer<TKey> comparer = null, int averageSizeInBytesOfKey = -1, int averageSizeInBytesOfValue = -1)
        {
            if (maximumSizeInMb < 1)
            {
                throw new ArgumentException("Cannot be less than one.", nameof(maximumSizeInMb));
            }

            long twoGbInBytes = (2048L*1024L*1024L);
            
            int entryTypeSize = TypeHelper.SizeOf(new FlashEntry(-1,default(TKey),default(TValue))) + (averageSizeInBytesOfKey > 0 ? averageSizeInBytesOfKey : 0) + (averageSizeInBytesOfValue > 0 ? averageSizeInBytesOfValue : 0);            
            long probableMaxArrayLength = twoGbInBytes/(entryTypeSize + 2);

            long desiredArrayLength = (maximumSizeInMb*(1024L)*(1024L))/entryTypeSize;

            if (desiredArrayLength > probableMaxArrayLength)
            {
                _maxItemCount = Convert.ToInt32(probableMaxArrayLength).FindNearestPowerOfTwoLessThan();
            }
            else
            {
                _maxItemCount = Convert.ToInt32(desiredArrayLength);
            }

            _sizeInMb = ((_maxItemCount * entryTypeSize)/1024)/1024;

            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries = new FlashEntry[_maxItemCount];          
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
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }

            int hashCode = GetHashcodeForKey(key);
            int index = GetTargetEntryIndexForHashcode(hashCode);

            FlashEntry entry = GetFlashEntryWithMemoryBarier(index);

            if (ReferenceEquals(entry, null) == false && KeyIsEqual(key, entry, hashCode))
            {
                return entry.Value;
            }

            TValue value = valueFactory.Invoke(key);

            InsertEntry(key, hashCode, value, index);

            return value;
        }
        /// <summary>
        /// Returns the current items in cache. Beware this is an O(n) operation.
        /// </summary>
        public int CacheItemCount
        {
            get
            {
                int itemCount = 0;
                for (int i = 0; i < _entries.Length; i++)
                {
                    FlashEntry flashEntry = _entries[i];
#if NET40
                    Thread.MemoryBarrier();
#else
                    Interlocked.MemoryBarrier();
#endif
                    if (!ReferenceEquals(flashEntry, null))
                    {
                        itemCount++;
                    }
                }
                return itemCount;
            }
        }

        /// <summary>
        /// The calculated maximum size in MB's that this cache should be.
        /// </summary>
        public int SizeInMb
        {
            get { return _sizeInMb; }
        }

        private FlashEntry GetFlashEntryWithMemoryBarier(int targetEntry)
        {
            FlashEntry entry = _entries[targetEntry]; //copy to local variable for thread safety
            //"volatile" read of value
#if NET40
            Thread.MemoryBarrier();
#else
            Interlocked.MemoryBarrier();
#endif
            return entry;
        }

        private bool KeyIsEqual(TKey key, FlashEntry entry, int hashCode)
        {
            return entry.HashCode == hashCode && _comparer.Equals(key, entry.Key);
        }

        private int GetTargetEntryIndexForHashcode(int hashCode)
        {
            return hashCode & (_maxItemCount - 1); // bitwise % operator since array is always length power of 2
        }

        private int GetHashcodeForKey(TKey key)
        {
            return _comparer.GetHashCode(key) & 0x7FFFFFFF; //lower 31 bits
        }

        private void InsertEntry(TKey key, int hashCode, TValue value, int targetEntry)
        {
            FlashEntry entryToInsert = new FlashEntry(hashCode ,key, value);
            //"volatile" read of value
#if NET40
            Thread.MemoryBarrier();
#else
            Interlocked.MemoryBarrier();
#endif
            _entries[targetEntry] = entryToInsert;
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="cacheKey">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the <see cref="FlashCache{TKey,TValue}"/> contains an element with the specified key; otherwise, false.</returns>
        /// <remarks>If the key is not found, then the value parameter gets the appropriate default value for the type TValue; for example, 0 (zero) for integer types, false for Boolean types, and null for reference types. </remarks>
        public bool TryGet(TKey cacheKey, out TValue value)
        {
            value = default(TValue);

            int hashCode = GetHashcodeForKey(cacheKey);
            int index = GetTargetEntryIndexForHashcode(hashCode);

            FlashEntry entry = GetFlashEntryWithMemoryBarier(index);

            if (ReferenceEquals(entry, null))
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

        private class FlashEntry
        {
            public readonly int HashCode; // Lower 31 bits of hash code 
            public readonly TKey Key;
            public readonly TValue Value;
        

            public FlashEntry(int hashCode, TKey key, TValue value)
            {
                HashCode = hashCode;
                Key = key;
                Value = value;
            }         
           
        }
    }
}