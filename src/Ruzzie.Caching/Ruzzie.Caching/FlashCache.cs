﻿using System;
using System.Collections.Generic;

namespace Ruzzie.Caching
{
    /// <summary>
    ///     Fixed size high performant in memory cache.
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
        private readonly int _indexMask;

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety.
        /// </summary>
        /// <param name="maximumSizeInMb">
        ///     The maximum desired size in MegaBytes of the cache. The cache size will be an
        ///     approximation of the size in Mb's.
        /// </param>
        /// <param name="comparer">Optionally the desired equality comparer to use for comparing keys.</param>
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
        public FlashCache(int maximumSizeInMb, IEqualityComparer<TKey> comparer = null, int averageSizeInBytesOfKey = -1,
            int averageSizeInBytesOfValue = -1)
        {
            if (maximumSizeInMb < 1)
            {
                throw new ArgumentException("Cannot be less than one.", nameof(maximumSizeInMb));
            }

            int flashEntryTypeSize = CalculateFlashEntryTypeSize(averageSizeInBytesOfKey, averageSizeInBytesOfValue);

            _maxItemCount = SizeHelper.CalculateMaxItemCountInPowerOfTwo(maximumSizeInMb, flashEntryTypeSize);
            _indexMask = _maxItemCount - 1;

            _sizeInMb = ((_maxItemCount*flashEntryTypeSize)/1024)/1024;

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
        ///     Returns the current items in cache. Beware this is an O(n) operation.
        /// </summary>
        public int CacheItemCount
        {
            get
            {
                var itemCount = 0;
                for (var i = 0; i < _entries.Length; i++)
                {
                    FlashEntry flashEntry = Volatile.Read(ref _entries[i]);
                    if (!ReferenceEquals(flashEntry, null))
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

        /// <summary>
        ///     Trims the cache. This method always returns 0 for <see cref="FlashCache{TKey,TValue}" />. Since it has a guaranteed
        ///     fixed size and needs no trimming.
        /// </summary>
        /// <param name="trimOptions">The trim options.</param>
        /// <returns>0</returns>
        public int Trim(TrimOptions trimOptions)
        {
            return 0; //no trim necesarry with this implementation.
        }

        internal static int CalculateFlashEntryTypeSize(int averageSizeInBytesOfKey = -1, int averageSizeInBytesOfValue = -1)
        {
            int entryTypeSize = TypeHelper.SizeOf(new FlashEntry(-1, default(TKey), default(TValue))) +
                                (averageSizeInBytesOfKey > 0 ? averageSizeInBytesOfKey : 0) +
                                (averageSizeInBytesOfValue > 0 ? averageSizeInBytesOfValue : 0);
            return entryTypeSize;
        }

        private FlashEntry GetFlashEntryWithMemoryBarier(int targetEntry)
        {
            return Volatile.Read(ref _entries[targetEntry]);
        }

        private bool KeyIsEqual(TKey key, FlashEntry entry, int hashCode)
        {
            return entry.HashCode == hashCode && _comparer.Equals(key, entry.Key);
        }

        private int GetTargetEntryIndexForHashcode(int hashCode)
        {
            unchecked
            {
                return (hashCode) & (_indexMask); // bitwise % operator since array is always length power of 2
            }
        }

        private int GetHashcodeForKey(TKey key)
        {
            return _comparer.GetHashCode(key);
        }

        private void InsertEntry(TKey key, int hashCode, TValue value, int targetEntry)
        {
            FlashEntry entryToInsert = new FlashEntry(hashCode, key, value);
            Volatile.Write(ref _entries[targetEntry], entryToInsert);
        }

        internal class FlashEntry
        {
            public readonly int HashCode;
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