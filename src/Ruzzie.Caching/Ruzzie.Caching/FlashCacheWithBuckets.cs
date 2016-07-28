using System;
using System.Collections.Generic;
using System.Threading;
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
        private const int TrimTimerInSeconds = 30;
        private readonly IEqualityComparer<TKey> _comparer;

        private readonly FlashEntry[] _entries;
        private readonly int _sizeInMb;
        private readonly int _maxItemCount;
        private readonly ConcurrentCircularOverwriteBuffer<int> _addedHashcodesRingBuffer;

        private readonly Timer _trimTimer;

        private long _currentItemCount;
        private readonly int _indexMask;

        private readonly object[] _locks;

        /// <summary>
        ///     Constructor. Creates the FlashCache of a fixed maximumSizeInMb.
        ///     The use is a fixed size cache. Items are NOT guaranteed to be cached forever. Locations will be overwritten based
        ///     on the hashcode.
        ///     This cache guarantees a fixed size and read and write thread safety. The cache will estimate the probable size of each type in the cache. 
        ///     The size calculation in general use cases is pessimistic. If you see a big difference in real memory usage and the size of the cache, tune it with the parameters or give a larger size.
        /// </summary>
        /// <param name="maximumSizeInMb">The maximum desired size in MegaBytes of the cache. The cache size will be an approximation of the size in Mb's.</param>
        /// <param name="comparer">Optionally the desired equality comparer to use for comparing keys.</param>
        /// <param name="averageSizeInBytesOfKey">Default -1. Only pass a value if the <typeparamref name="TKey"/> is a value type. This parameter takes the given bytes for calculating the maximum size of the cache.</param>
        /// <param name="averageSizeInBytesOfValue">Default -1. Only pass a value if the <typeparamref name="TValue"/> is a value type. This parameter takes the given bytes for calculating the maximum size of the cache.</param>
        /// <remarks>The size in Mb's is an estimation. If the key or value for the cache is a reference type, it does not take into account the memory space the data of the reference type hold by default. All lookups in the cache are an O(1) operation.
        /// The maximum size of the Cache object itself is guaranteed.
        /// </remarks>
        public FlashCacheWithBuckets(int maximumSizeInMb, IEqualityComparer<TKey> comparer = null, int averageSizeInBytesOfKey = -1, int averageSizeInBytesOfValue = -1)
        {
            //TODO: Modify APi to be more clear about default collection and string sizes and the extra size parameters, and add something about the trim timing
            if (maximumSizeInMb < 1)
            {
                throw new ArgumentException("Cannot be less than one.", nameof(maximumSizeInMb));
            }

            int flashEntryTypeSize = CalculateFlashEntryTypeSize(averageSizeInBytesOfKey, averageSizeInBytesOfValue);

            _maxItemCount = SizeHelper.CalculateMaxItemCountInPowerOfTwo(maximumSizeInMb, flashEntryTypeSize);

            _indexMask = _maxItemCount - 1;

            _sizeInMb = ((_maxItemCount * flashEntryTypeSize) / 1024) / 1024;

            _comparer = comparer ?? EqualityComparer<TKey>.Default;
            _entries = new FlashEntry[_maxItemCount];
            _locks = new object[_maxItemCount];
            InitializeLockArray();

            _addedHashcodesRingBuffer = new ConcurrentCircularOverwriteBuffer<int>(_maxItemCount);
            _trimTimer = new Timer(TrimTimerCallback, this, new TimeSpan(0, 0, 0, TrimTimerInSeconds), new TimeSpan(0, 0, 0, TrimTimerInSeconds));
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

        private bool _disposed;
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

        internal static int CalculateFlashEntryTypeSize(int averageSizeInBytesOfKey = -1, int averageSizeInBytesOfValue = -1)
        {
            int entryTypeSize = TypeHelper.SizeOf(new FlashEntry(-1, default(TKey), default(TValue))) +
                                (averageSizeInBytesOfKey > 0 ? averageSizeInBytesOfKey : 0) +
                                (averageSizeInBytesOfValue > 0 ? averageSizeInBytesOfValue : 0);
            return entryTypeSize;
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
        public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
        {
            if (valueFactory == null)
            {
                throw new ArgumentNullException(nameof(valueFactory));
            }

            int hashCode;
            int index;
            FlashEntry entry;

            if (TryGetInternal(key, out entry, out hashCode, out index))
            {         
                return entry.Value;
            }

            FlashEntry nextEntry;            

            lock (_locks[index])
            {
                GetFlashEntryUnsafe(index, ref entry);

                if (entry != null && KeyIsEqual(key, hashCode, entry))
                {
                    return entry.Value;
                }

                nextEntry = entry;

                while (nextEntry?.Next != null)
                {
                    nextEntry = nextEntry.Next;

                    if (KeyIsEqual(key, hashCode, nextEntry))
                    {
                        return nextEntry.Value;
                    }
                }

                TValue value = valueFactory.Invoke(key);

                InsertEntry(key, hashCode, value, index, ref nextEntry, ref entry);
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

        /// <summary>
        /// The calculated maximum size in MB's that this cache should be.
        /// </summary>
        public int SizeInMb
        {
            get { return _sizeInMb; }
        }

        // ReSharper disable once RedundantAssignment
        private void GetFlashEntryUnsafe(int index, ref FlashEntry entry)
        {
            entry = _entries[index];            
        }

        private bool KeyIsEqual(TKey key,  int hashCode, FlashEntry entryToCompareTo)
        {
            return entryToCompareTo.HashCode == hashCode && _comparer.Equals(key, entryToCompareTo.Key);
        }

        private int GetTargetEntryIndexForHashcode(int hashCode)
        {
            return (hashCode) & (_indexMask); // bitwise % operator since array is always length in power of 2
        }

        private int GetHashcodeForKey(TKey key)
        {
            return _comparer.GetHashCode(key);
        }

        //Assume access to parent entry is ThreadSafe. The caller of this method is responsible for locking.
        private void InsertEntry(TKey key, int hashCode, TValue value, int targetEntryIndex, ref FlashEntry entryToAddTo, ref FlashEntry entry)
        {
            FlashEntry entryToInsert = new FlashEntry(hashCode, key, value);
            _addedHashcodesRingBuffer.WriteNext(hashCode);

            if (entry == null)
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
            if (ReferenceEquals(entry, entryToAddTo) || (entryToAddTo == null))
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

        private void TrimTimerCallback(object state)
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

        internal int TrimCache(int trimSize, TrimOptions options = TrimOptions.Default)
        {
            if (_currentItemCount < _maxItemCount)
            {
                return 0;
            }
            
            int trimCount = 0;
            while (trimCount < trimSize)
            {
                int hashCode;
                if (!_addedHashcodesRingBuffer.ReadNext(out hashCode))
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
                    FlashEntry entry = null;
                    GetFlashEntryUnsafe(index, ref entry);

                    if (entry != null)
                    {
                        FlashEntry currentEntry = entry;
                        FlashEntry previousEntry = null;
                       
                        while (currentEntry?.Next != null)
                        {
                            previousEntry = currentEntry;
                            currentEntry = currentEntry?.Next;
                        }

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                        // ReSharper disable once ConstantConditionalAccessQualifier
                        if (entry?.Next == null)
                            // ReSharper disable HeuristicUnreachableCode
                        {
                            _entries[index] = null;
                            trimCount++;
                        }
                        // ReSharper restore HeuristicUnreachableCode
                        else
                        {
                            //remove last item in chain
                            if (previousEntry != null)
                            {
                                previousEntry.Next = null;
                                _entries[index] = entry;
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

                        if (flashEntry != null)
                        {
                            itemCount++;
                            FlashEntry next = flashEntry.Next;
                            while (next != null)
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
        public bool TryGet(TKey key, out TValue value)
        {
            int hashCode;
            int index;

            FlashEntry entry;
            bool result = TryGetInternal(key, out entry, out hashCode, out index);
            if (result)
            {
                value = entry.Value;
                return true;
            }

            value = default(TValue);
            return false;
        }

        private bool TryGetInternal(TKey key, out FlashEntry entry, out int hashCode, out int index)
        {
            hashCode = GetHashcodeForKey(key);
            index = GetTargetEntryIndexForHashcode(hashCode);
   
            entry = GetFlashEntryWithMemoryBarrier(index);

            if (entry != null)
            {
                if (KeyIsEqual(key, hashCode, entry))
                {
                    return true;
                }

                FlashEntry nextEntry = entry;

                while (nextEntry != null)
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
        public int Trim(TrimOptions trimOptions)
        {
            if (CacheItemCount < MaxItemCount)
            {
                return 0;
            }

            int realItemCount = RealCacheItemCount;
            switch (trimOptions)
            {
                case TrimOptions.Default:
                    return TrimCache((int) (_maxItemCount*0.05), trimOptions);
                case TrimOptions.Aggressive:
                    if (realItemCount > MaxItemCount)
                    {
                        return TrimCache(Math.Max(realItemCount - _maxItemCount, 0),trimOptions);
                    }
                    break;
                case TrimOptions.Cautious:
                    return TrimCache(2, trimOptions);
                default:
                    return 0;
            }
            return 0;
        }

        private FlashEntry GetFlashEntryWithMemoryBarrier(int index)
        {
            return Common.Threading.Volatile.Read(ref _entries[index]);
        }

        private class FlashEntry
        {
            public readonly int HashCode;
            public readonly TKey Key;
            public readonly TValue Value;
            public volatile FlashEntry Next;

            public FlashEntry(int hashCode, TKey key, TValue value, FlashEntry next = null)
            {
                HashCode = hashCode;
                Key = key;
                Value = value;
                Next = next;
            }
        }
    }
}