using Ruzzie.Common.Numerics;
#if !NETSTANDARD1_1
using System;
using System.Collections.Generic;
using System.Threading;
using Ruzzie.Common;
using Ruzzie.Common.Collections;
#if !PORTABLE
using System.Threading.Tasks;
#endif

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
    public class FlashCacheWithBuckets<TKey, TValue> : IFixedSizeCache<TKey, TValue>, IDisposable
    {
        private const    int                     TrimTimerInSeconds = 30;
        private readonly IEqualityComparer<TKey> _comparer;

        private readonly FlashEntry[]                           _entries;
        private readonly int                                    _maxItemCount;
        private readonly ConcurrentCircularOverwriteBuffer<int> _addedHashCodesRingBuffer;

        private readonly Timer _trimTimer;

        private          long _currentItemCount;
        private readonly int  _indexMask;

        private readonly object[] _locks;

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety.
        /// </summary>
        /// <param name="comparer">Optionally the desired equality comparer to use for comparing keys.</param>
        /// <param name="maxItemCount">The (fixed) number of items this cache can hold.</param>
        /// <remarks>All lookups in the cache are an O(1) operation.
        /// The maximum size of the Cache object itself is guaranteed.
        /// </remarks>
        public FlashCacheWithBuckets(IEqualityComparer<TKey> comparer, int maxItemCount)
        {
            if (maxItemCount < 2)
            {
                throw new ArgumentException("Cannot be less than 2.", nameof(maxItemCount));
            }

            _maxItemCount = maxItemCount.FindNearestPowerOfTwoEqualOrLessThan();

            _indexMask = _maxItemCount - 1;

            // ReSharper disable once NullCoalescingConditionIsAlwaysNotNullAccordingToAPIContract
            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries  = new FlashEntry[_maxItemCount];
            _locks    = new object[_maxItemCount];
            InitializeLockArray();

            _addedHashCodesRingBuffer = new ConcurrentCircularOverwriteBuffer<int>(_maxItemCount);
            _trimTimer = new Timer(TrimTimerCallback
                                 , this
                                 , new TimeSpan(0, 0, 0, TrimTimerInSeconds)
                                 , new TimeSpan(0, 0, 0, TrimTimerInSeconds));
        }

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety.
        /// </summary>
        /// <param name="maxItemCount">The (fixed) number of items this cache can hold.</param>
        /// <remarks>All lookups in the cache are an O(1) operation.
        /// The maximum size of the Cache object itself is guaranteed.
        /// </remarks>
        public FlashCacheWithBuckets(int maxItemCount) : this(EqualityComparer<TKey>.Default, maxItemCount)
        {
        }

        private void InitializeLockArray()
        {
#if !PORTABLE
            Parallel.For(0, _maxItemCount, i => _locks[i] = new object());
#else
            for (int i = 0; i < _maxItemCount; i++)
            {
              _locks[i] = new object();
            }
#endif
        }

        private          bool   _disposed;
        private readonly Random _random = new SimpleRandom(Environment.TickCount);

        /// <summary>
        /// Disposed unmanaged resources.
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                // Dispose of resources held by this instance.
                _trimTimer?.Dispose();

                _disposed = true;
                // Suppress finalization of this disposed instance.
                if (disposing)
                {
                    GC.SuppressFinalize(this);
                }
            }
        }

        /// <summary>
        /// Disposed unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Dispose of resources held by this instance.
                Dispose(true);
            }
        }

        /// <summary>
        /// Allows an object to try to free resources and perform other cleanup operations before it is reclaimed by garbage collection.
        /// </summary>
        ~FlashCacheWithBuckets()
        {
            Dispose(false);
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

            int        hashCode;
            int        index;
            FlashEntry entry;

            if (TryGetInternal(key, out entry, out hashCode, out index))
            {
                return entry.Value;
            }

            FlashEntry nextEntry;

            lock (_locks[index])
            {
                entry = GetFlashEntryUnsafe(index);

                if (entry != FlashEntry.Empty && KeyIsEqual(key, hashCode, entry))
                {
                    return entry.Value;
                }

                nextEntry = entry;

                while (nextEntry.Next != FlashEntry.Empty && nextEntry.Next != null)
                {
                    nextEntry = nextEntry.Next;

                    if (KeyIsEqual(key, hashCode, nextEntry))
                    {
                        return nextEntry.Value;
                    }
                }

                TValue value = valueFactory.Invoke(key);

                InsertEntry(key, hashCode, value, index, nextEntry, entry);
                return value;
            }
        }

        //TODO: add warning
        /// <summary>
        /// Returns the current items in cache. 
        /// </summary>
        public int CacheItemCount
        {
            get { return RealCacheItemCount; }
        }

        // ReSharper disable once RedundantAssignment
        private FlashEntry GetFlashEntryUnsafe(in int index)
        {
            return _entries[index] ?? FlashEntry.Empty;
        }

        private bool KeyIsEqual(in TKey key, in int hashCode, in FlashEntry entryToCompareTo)
        {
            return entryToCompareTo.HashCode == hashCode && _comparer.Equals(key, entryToCompareTo.Key);
        }

        private int GetTargetEntryIndexForHashcode(in int hashCode)
        {
            return (hashCode) & (_indexMask); // bitwise % operator since array is always length in power of 2
        }

        private int GetHashcodeForKey(in TKey key)
        {
            return key == null ? 0 : _comparer.GetHashCode(key);
        }

        //Assume access to parent entry is ThreadSafe. The caller of this method is responsible for locking.
        private void InsertEntry(in TKey    key
                               , in int     hashCode
                               , in TValue  value
                               , in int     targetEntryIndex
                               , FlashEntry entryToAddTo
                               , FlashEntry entry)
        {
            FlashEntry entryToInsert = new FlashEntry(hashCode, key, value);
            _addedHashCodesRingBuffer.WriteNext(hashCode);

            //No existing 'Bucket' add new entry
            if (entry == FlashEntry.Empty)
            {
                _entries[targetEntryIndex] = entryToInsert;
                if (_currentItemCount < _maxItemCount)
                {
                    Interlocked.Increment(ref _currentItemCount);
                }
                else
                {
                    //remove some stuff??
                    //TrimCache(1);                    
                }

                return;
            }

            //Entry with no next values
            if (ReferenceEquals(entry, entryToAddTo) || entryToAddTo == FlashEntry.Empty)
            {
                if (_currentItemCount >= _maxItemCount)
                {
                    entry = entryToInsert;
                }
                else
                {
                    entry.Next = entryToInsert;
                    Interlocked.Increment(ref _currentItemCount);
                }
            }
            else
            {
                //Entry with next values
                if (_currentItemCount >= _maxItemCount)
                {
                    entryToAddTo = entryToInsert;
                }
                else
                {
                    entryToAddTo.Next = entryToInsert;
                    Interlocked.Increment(ref _currentItemCount);
                }
            }
        }

        private void TrimTimerCallback(object? state)
        {
            try
            {
                _trimTimer.Change(Timeout.Infinite, Timeout.Infinite);
                Trim(TrimOptions.Aggressive);
            }
            finally
            {
                _trimTimer.Change(new TimeSpan(0, 0, 0, TrimTimerInSeconds), new TimeSpan(0, 0, 0, TrimTimerInSeconds));
            }
        }

        internal int TrimCache(in int trimSize, in TrimOptions options = TrimOptions.Default)
        {
            if (_currentItemCount < _maxItemCount)
            {
                return 0;
            }

            int trimCount = 0;
            while (trimCount < trimSize)
            {
                int hashCode;
                if (!_addedHashCodesRingBuffer.ReadNext(out hashCode))
                {
                    if (options == TrimOptions.Aggressive)
                    {
                        //boot some random item out of the cache
                        hashCode = _random.Next();
                    }
                    else
                    {
                        break;
                    }
                }

                int index = GetTargetEntryIndexForHashcode(hashCode);

                lock (_locks[index])
                {
                    FlashEntry entry = GetFlashEntryUnsafe(index);

                    if (entry != FlashEntry.Empty)
                    {
                        FlashEntry currentEntry  = entry;
                        FlashEntry previousEntry = FlashEntry.Empty;

                        while (currentEntry.Next != FlashEntry.Empty)
                        {
                            previousEntry = currentEntry;
                            currentEntry  = currentEntry.Next;
                        }

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        // ReSharper disable once ConstantConditionalAccessQualifier
                        if (entry.Next == FlashEntry.Empty)
                            // ReSharper disable HeuristicUnreachableCode
                        {
                            _entries[index] = FlashEntry.Empty;
                            trimCount++;
                        }
                        // ReSharper restore HeuristicUnreachableCode
                        else
                        {
                            //remove last item in chain
                            if (previousEntry != FlashEntry.Empty)
                            {
                                previousEntry.Next = FlashEntry.Empty;
                                _entries[index]    = entry;
                                trimCount++;
                            }
                        }
                    }
                }
            }

            return trimCount;
        }

        /// <summary>
        /// Gets the real cache item count.
        /// </summary>
        /// <value>
        /// The real cache item count.
        /// </value>
        internal int RealCacheItemCount
        {
            get
            {
                int itemCount = 0;
                int nextCount = 0;
                for (int i = 0; i < _entries.Length; i++)
                {
                    lock (_locks[i])
                    {
                        FlashEntry flashEntry = _entries[i];

                        if (flashEntry != FlashEntry.Empty && flashEntry != null)
                        {
                            itemCount++;
                            FlashEntry next = flashEntry.Next;
                            while (next != FlashEntry.Empty && next != null)
                            {
                                nextCount++;
                                next = next.Next;
                            }
                        }
                    }
                }

                return itemCount + nextCount;
            }
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key of the value to get.</param>
        /// <param name="value">When this method returns, contains the value associated with the specified key, if the key is found; otherwise, the default value for the type of the value parameter. This parameter is passed uninitialized.</param>
        /// <returns>true if the <see cref="FlashCache{TKey,TValue}"/> contains an element with the specified key; otherwise, false.</returns>
        /// <remarks>If the key is not found, then the value parameter gets the appropriate default value for the type TValue; for example, 0 (zero) for integer types, false for Boolean types, and null for reference types. </remarks>
        public bool TryGet(in TKey key, out TValue value)
        {
            FlashEntry entry;
            bool       result = TryGetInternal(key, out entry, out _, out _);
            if (result)
            {
                value = entry.Value;
                return true;
            }

            value = default(TValue)!;
            return false;
        }

        private bool TryGetInternal(in TKey key, out FlashEntry entry, out int hashCode, out int index)
        {
            hashCode = GetHashcodeForKey(key);
            index    = GetTargetEntryIndexForHashcode(hashCode);

            entry = GetFlashEntryWithMemoryBarrier(index);

            if (entry != FlashEntry.Empty)
            {
                if (KeyIsEqual(key, hashCode, entry))
                {
                    return true;
                }

                FlashEntry nextEntry = entry;

                while (nextEntry != FlashEntry.Empty)
                {
                    entry = nextEntry;
                    if (KeyIsEqual(key, hashCode, nextEntry))
                    {
                        return true;
                    }

                    nextEntry = nextEntry.Next;
                }
            }

            return false;
        }

        /// <summary>
        /// Trims the cache when is has grown to big. This method is for callers who want to have more control over the cache size. Implementors of this interface are responsible for keeping the cachesize as fixed as possible.
        /// When this method does nothing or no items are removed return 0.
        /// </summary>
        /// <param name="trimOptions">The trim options to use when trimming.</param>
        /// <returns>The number of items removed from the cache.</returns>
        public int Trim(in TrimOptions trimOptions)
        {
            if (CacheItemCount < MaxItemCount)
            {
                return 0;
            }

            int realItemCount = RealCacheItemCount;
            switch (trimOptions)
            {
                case TrimOptions.Default:
                    return TrimCache((int)(_maxItemCount * 0.05), trimOptions);
                case TrimOptions.Aggressive:
                    if (realItemCount > MaxItemCount)
                    {
                        return TrimCache(Math.Max(realItemCount - _maxItemCount, 0), trimOptions);
                    }

                    break;
                case TrimOptions.Cautious:
                    return TrimCache(2, trimOptions);
                default:
                    return 0;
            }

            return 0;
        }

        private FlashEntry GetFlashEntryWithMemoryBarrier(in int index)
        {
            return Common.Threading.Volatile.Read(ref _entries[index]) ?? FlashEntry.Empty;
        }

        private class FlashEntry
        {
            public static readonly FlashEntry Empty = new FlashEntry(-1, default!, default!, null!);
            public readonly        int        HashCode;
            public readonly        TKey       Key;
            public readonly        TValue     Value;
            public volatile        FlashEntry Next;

            public FlashEntry(in int hashCode, in TKey key, in TValue value, FlashEntry next)
            {
                HashCode = hashCode;
                Key      = key;
                Value    = value;
                Next     = next ?? Empty;
            }

            public FlashEntry(in int hashCode, in TKey key, in TValue value)
            {
                HashCode = hashCode;
                Key      = key;
                Value    = value;
                Next     = Empty;
            }
        }
    }
}
#endif