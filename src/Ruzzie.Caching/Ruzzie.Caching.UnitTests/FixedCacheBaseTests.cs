using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests;

public abstract class FixedCacheBaseTests : CacheEfficiencyTests
{
    private IFixedSizeCache<int, int> _flashCacheShouldCacheSameValues;

    [Test]
    public void Int32FixedSize()
    {
        CacheShouldStayFixedSize(i => i - (int.MaxValue /2));
    }

    [Test]
    public void StringFixedSize()
    {
        CacheShouldStayFixedSize(i => i.ToString().PadLeft(20, '0'));
    }

    protected void CacheShouldStayFixedSize<T>(Func<int, T> keyFactory, int? customCacheItemCountToAssert = null)
    {
        IFixedSizeCache<T, byte> cache                 = CreateCache<T, byte>(131072);
        int                      numberOfItemsToInsert = cache.MaxItemCount*2; //add twice the items the cache can hold.
        for (var i = 0; i < numberOfItemsToInsert; i++)
        {
            cache.GetOrAdd(keyFactory.Invoke(i), key => 1);
        }
        cache.Trim(TrimOptions.Aggressive);
        Assert.That(cache.CacheItemCount, Is.LessThanOrEqualTo(customCacheItemCountToAssert ?? cache.MaxItemCount),
                    "Cache size does not seem limited by maxItemCount for: " + typeof (T));
        (cache as IDisposable)?.Dispose();
    }

    [Test]
    public void GetOrAddThrowsArgumentNullExceptionWhenKeyIsNull()
    {
        Assert.That(() => CreateCache<string, string>(16).GetOrAdd("1", null), Throws.TypeOf<ArgumentNullException>());
    }

    [Test]
    public void GetOrAddShouldCacheItem()
    {
        IFixedSizeCache<string, int> cache               = CreateCache<string, int>(1024);
        var                          numberOfTimesCalled = 0;

        //Once
        cache.GetOrAdd("key", key =>
                              {
                                  numberOfTimesCalled++;
                                  return numberOfTimesCalled;
                              });

        //Twice
        cache.GetOrAdd("key", key =>
                              {
                                  numberOfTimesCalled++;
                                  return numberOfTimesCalled;
                              });

        Assert.That(numberOfTimesCalled, Is.EqualTo(1));
    }

    [TestCase(1,    1)]
    [TestCase(2,    2)]
    [TestCase(10,   10)]
    [TestCase(100,  98)]
    [TestCase(500,  480)] //misses due to poor hash spreading icm. pow 2
    [TestCase(1024, 980)] //misses due to poor hash spreading icm. pow 2
    public void CacheItemCountShouldReturnOnlyItemsInCache(int numberOfItemsToInsert, int expectedCount)
    {
        IFixedSizeCache<string, Guid> cache = CreateCache<string, Guid>(131072);

        for (var i = 0; i < numberOfItemsToInsert; i++)
        {
            Guid newGuid = Guid.NewGuid();
            cache.GetOrAdd(i + "CacheItemCountShouldReturnOnlyItemsInCache", key => newGuid);
        }
        Debug.WriteLine(cache.MaxItemCount.ToString());
        Assert.That(cache.CacheItemCount, Is.GreaterThanOrEqualTo(expectedCount));
        (cache as IDisposable)?.Dispose();
    }

    [TestCase(1,    1)]
    [TestCase(2,    2)]
    [TestCase(10,   10)]
    [TestCase(100,  98)]  //misses due to poor hash spreading icm. pow 2
    [TestCase(500,  488)] //misses due to poor hash spreading icm. pow 2
    [TestCase(1024, 988)] //misses due to poor hash spreading icm. pow 2
    public void CacheItemCountShouldReturnOnlyItemsInCacheWithGuidAsKey(int numberOfItemsToInsert, int expectedCount)
    {
        IFixedSizeCache<Guid, Guid> cache = CreateCache<Guid, Guid>(131072);

        for (var i = 0; i < numberOfItemsToInsert; i++)
        {
            Guid newGuid = Guid.NewGuid();
            cache.GetOrAdd(newGuid, key => newGuid);
        }
        Debug.WriteLine(cache.MaxItemCount.ToString());
        Assert.That(cache.CacheItemCount, Is.GreaterThanOrEqualTo(expectedCount));
        (cache as IDisposable)?.Dispose();
    }

    [Test]
    public void GetOrAddShouldReturnValueFactoryResult()
    {
        IFixedSizeCache<string, int> cache = CreateCache<string, int>(1024);

        int value = cache.GetOrAdd("key", key => 1);

        Assert.That(value, Is.EqualTo(1));
    }

    [Test]
    public void InitializeWithSizeLessThanOneShouldThrowArgumentException()
    {
        Assert.That(() => CreateCache<int, int>(0), Throws.ArgumentException);
    }

    [Test]
    public void OverwriteCacheLocationWithSameHash()
    {
        IFixedSizeCache<KeyTestTypeWithConstantHash, int> flashCache = CreateCache<KeyTestTypeWithConstantHash, int>(16);

        flashCache.GetOrAdd(new KeyTestTypeWithConstantHash {Value = "10"}, i => 10);
        int value = flashCache.GetOrAdd(new KeyTestTypeWithConstantHash {Value = "99"}, i => 99);

        Assert.That(value, Is.EqualTo(99));
    }

    [Test]
    public void ShouldCacheSameValue()
    {
        IFixedSizeCache<string, int> flashCache = CreateCache<string, int>(1024);

        flashCache.GetOrAdd("10", i => 10);
        int value = flashCache.GetOrAdd("10", i => 99);

        Assert.That(value, Is.EqualTo(10));
    }

    [OneTimeSetUp]
    public void FixtureSetUp()
    {
        _flashCacheShouldCacheSameValues = CreateCache<int, int>(131072);
    }

    [Test]
    [TestCase(0,          0,          0)]
    [TestCase(1,          1,          1)]
    [TestCase(1024,       1024,       1024)]
    [TestCase(523,        523,        523)]
    [TestCase(1073741824, 1073741824, 1073741824)]
    public void ShouldCacheSameValues(int key, int valueToStore, int expected)
    {
        _flashCacheShouldCacheSameValues.GetOrAdd(key, i => valueToStore);
        int value = _flashCacheShouldCacheSameValues.GetOrAdd(key, i => 99);

        Assert.That(value, Is.EqualTo(expected));
    }

    [Test]
    public void ShouldInitializeWithComparer()
    {
        // ReSharper disable once ObjectCreationAsStatement
        IFixedSizeCache<string, string> cache = CreateCache<string, string>(10, StringComparer.OrdinalIgnoreCase);
        (cache as IDisposable)?.Dispose();
    }

    [Test]
    public void ShouldInitializeWithFixedMaximumSizeToNearestPowerOfTwo()
    {
        IFixedSizeCache<string, string> cache = CreateCache<string, string>(4194344);

        Assert.That(cache.MaxItemCount, Is.EqualTo(4194304));
        (cache as IDisposable)?.Dispose();
    }

    [Test]
    public void ShouldInitializeWithPassedItemCount()
    {
        var cache = CreateCache<string, string>(8192);

        Assert.That(cache.MaxItemCount, Is.EqualTo(8192));
    }

    [TestCase(4)]
    [TestCase(20)]
    [TestCase(100)]
    [TestCase(512)]
    [TestCase(31235)]
    public void MaxItemCountShouldBeLessOrEqualToGivenMaxItemCount(int givenMax)
    {
        IFixedSizeCache<string, string> cache = CreateCache<string, string>(givenMax);

        Assert.That(cache.MaxItemCount, Is.LessThanOrEqualTo(givenMax));
        (cache as IDisposable)?.Dispose();
    }

    [Test]
    public void InsertTwoItemsTest()
    {
        var keyOne = "No hammertime for: 19910";
        var keyTwo = "No hammertime for: 90063";

        IFixedSizeCache<string, string> cache = CreateCache<string, string>(1024);

        Assert.That(cache.GetOrAdd(keyOne, s => keyOne), Is.EqualTo(keyOne));
        Assert.That(cache.GetOrAdd(keyTwo, s => keyTwo), Is.EqualTo(keyTwo));
    }

    [Test]
    public void TryGetShouldReturnTrueWhenValueIsInCache()
    {
        IFixedSizeCache<string, int> cache = CreateCache<string, int>(1024);

        cache.GetOrAdd("1", s => 1);
        int value;
        Assert.That(cache.TryGet("1", out value), Is.True);
    }

    [Test]
    public void TryGetShouldSetOutValueWhenValueIsInCache()
    {
        IFixedSizeCache<string, int> cache = CreateCache<string, int>(1024);

        cache.GetOrAdd("1", s => 1);
        int value;
        cache.TryGet("1", out value);

        Assert.That(value, Is.EqualTo(1));
    }

    [Test]
    public void TryGetShouldReturnFalseWhenValueIsNotInCache()
    {
        IFixedSizeCache<string, int> cache = CreateCache<string, int>(1024);

        int value;

        Assert.That(cache.TryGet("1", out value), Is.False);
    }

    [Test]
    public void TryGetShouldSetOutValueToDefaultWhenValueIsNotInCache()
    {
        IFixedSizeCache<string, int> cache = CreateCache<string, int>(1024);

        int value;
        cache.TryGet("1", out value);

        Assert.That(value, Is.EqualTo(0));
    }

    [Test]
    public void TryGetShouldReturnFalseForItemWithSameHashcodeAndDifferentValues()
    {
        IFixedSizeCache<KeyTestTypeWithConstantHash, string> cache = CreateCache<KeyTestTypeWithConstantHash, string>(1024);
        cache.GetOrAdd(new KeyTestTypeWithConstantHash {Value = "a"}, hash => "1");

        string value;
        Assert.That(cache.TryGet(new KeyTestTypeWithConstantHash {Value = "b"}, out value), Is.False);
    }
#if HAVE_PARALLELPERFORMANCE
    [Test]
    public void MultiThreadedReadWriteTest()
    {
        IFixedSizeCache<string, string> cache = CreateCache<string, string>(131072, new StringComparerOrdinalIgnoreCaseFNV1AHash());
        int maxItemCount = cache.MaxItemCount;

        Parallel.For(0, maxItemCount, new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount * 8}, i =>
                                                                                                                     {
                                                                                                                         string key = i.ToString().PadLeft(20, 'C');

                                                                                                                         Assert.That(cache.GetOrAdd(key, s => i.ToString()), Is.EqualTo(i.ToString()));
                                                                                                                         Assert.That(cache.GetOrAdd("A".PadLeft(20, '1'), s => 42.ToString()), Is.EqualTo(42.ToString()));
                                                                                                                     });

        cache.Trim(TrimOptions.Aggressive);
        //Cache size should be between The current item count * Efficiency and less than MaxItemCount
        Assert.That(cache.CacheItemCount,
                    Is.GreaterThanOrEqualTo(maxItemCount*(MinimalEfficiencyInPercent/100.0)).And.LessThanOrEqualTo(maxItemCount));
    }

    [Test]
    [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
    [TestCase(500)]
    public void MultiThreadedTest(int runtimeInMillis = 500)
    {
        IFixedSizeCache<string, byte> cache = null;

        //loop the cache size and assert a few times to check for bugs
        for (var k = 0; k < 5; k++)
        {
            cache = CreateCache<string, byte>(131072, StringComparer.OrdinalIgnoreCase);
            //first fill cache
            Parallel.For(0, cache.MaxItemCount, i => { cache.GetOrAdd(i.ToString().PadRight(20, 'M'), key => 1); });

            //Cache size should be between The current item count * Efficiency and less than MaxItemCount
            Assert.That(cache.CacheItemCount,
                        Is.GreaterThanOrEqualTo(cache.MaxItemCount*(MinimalEfficiencyInPercent/100.0)).And.LessThanOrEqualTo(cache.MaxItemCount));
        }
            
        var mustLoop = true;

        //write loops
        //Continuously write to the buffer
        Task writeLoopOne = Task.Run(() => { WriteToCacheLoop(ref mustLoop, cache); });

        Task writeLoopTwo = Task.Run(() => { WriteToCacheLoop(ref mustLoop, cache); });

        //Continuously aggressively trim
        Task trimLoop = Task.Run(() =>
                                 {
                                     while (mustLoop)
                                     {
                                         // ReSharper disable once PossibleNullReferenceException                    
                                         cache.Trim(TrimOptions.Aggressive);
                                     }
                                 });

        Thread.Sleep(runtimeInMillis);
        mustLoop = false;
        writeLoopOne.Wait();
        writeLoopTwo.Wait();
        trimLoop.Wait();
        //no exception should have occurred. and the size should be fixed

        //Cache size should be between The current item count * Efficiency and less than MaxItemCount
        // ReSharper disable once PossibleNullReferenceException
        cache.Trim(TrimOptions.Aggressive);
          
        Assert.That(cache.CacheItemCount,
                    Is.GreaterThanOrEqualTo(cache.MaxItemCount*(MinimalEfficiencyInPercent/100.0)).And.LessThanOrEqualTo(cache.MaxItemCount));
    }

    private static void WriteToCacheLoop(ref bool mustLoop, IFixedSizeCache<string, byte> cache)
    {
        Stopwatch timer   = new Stopwatch();
        var       counter = 0;
        timer.Start();
        while (mustLoop)
        {
            // ReSharper disable once PossibleNullReferenceException
            cache.GetOrAdd(counter.ToString().PadLeft(20, 'F'), key => 1);
            counter++;
        }
        timer.Stop();

        string message = "Total write calls: " + counter;
        message += "\n" + "Avg timer per write call: " + timer.Elapsed.TotalMilliseconds     /counter + " ms.";
        message += "\n" + "Avg timer per write call: " + (double) (timer.Elapsed.Ticks *100) /counter + " ns.";
        Trace.WriteLine(message);
    }
#endif
    [Test]
    public void TrimShouldRemoveExcessEntriesWhenThereAreExcessEntries()
    {
        IFixedSizeCache<string, int> cache = CreateCache<string, int>(16);
        //overfill cache (that is why we use string, since the hashspreading in pow2 is poor

        for (var i = 0; i < cache.MaxItemCount *2; i++)
        {
            int value = i;
            cache.GetOrAdd(i.ToString(), key => value);
        }

        cache.Trim(TrimOptions.Aggressive);

        Assert.That(cache.CacheItemCount,
                    Is.GreaterThanOrEqualTo(cache.MaxItemCount*(MinimalEfficiencyInPercent/100.0)).And.LessThanOrEqualTo(cache.MaxItemCount));
    }

    protected class KeyTestTypeWithConstantHash
    {
        public string Value;

        public override int GetHashCode()
        {
            return 7;
        }

        protected bool Equals(KeyTestTypeWithConstantHash other)
        {
            return string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }
            if (obj.GetType() != GetType())
            {
                return false;
            }
            return Equals((KeyTestTypeWithConstantHash) obj);
        }

        public static bool operator ==(KeyTestTypeWithConstantHash left, KeyTestTypeWithConstantHash right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(KeyTestTypeWithConstantHash left, KeyTestTypeWithConstantHash right)
        {
            return !Equals(left, right);
        }
    }
}