using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests;

[TestFixture]
public class FlashCacheWithBucketsTests : FixedCacheBaseTests
{
    [Test]
    public void SmokeTest()
    {
        FlashCacheWithBuckets<string,byte> cache = new FlashCacheWithBuckets<string,byte>(StringComparer.OrdinalIgnoreCase, 32768);

        Assert.That(cache.GetOrAdd("1", s => 1), Is.EqualTo((byte) 1));
        Assert.That(cache.CacheItemCount,        Is.EqualTo(1));
    }

    [Test]
    public void ItemsWithSameIndexShouldStillBeStoredWhenEnoughCapacity()
    {
        FlashCacheWithBuckets<int, byte> cache = new FlashCacheWithBuckets<int, byte>(32768);

        Debug.WriteLine(cache.MaxItemCount);

        cache.GetOrAdd(524288, i => (byte)1);          //524288 mod (32768) == 0
        byte itemTwo = cache.GetOrAdd(262144, i => 2); //262144 mod (32768) == 0

        Assert.That(cache.GetOrAdd(524288, i => 2), Is.EqualTo((byte) 1));
        Assert.That(itemTwo,                        Is.EqualTo((byte) 2));
        Assert.That(cache.RealCacheItemCount,       Is.EqualTo(2));
    }

    [Test]
    public void ShouldOverwriteBucketWhenValueIsUpdated()
    {
        FlashCacheWithBuckets<int, byte> cache = new FlashCacheWithBuckets<int, byte>(32768);

        Debug.WriteLine(cache.MaxItemCount);

        byte itemOne = cache.GetOrAdd(524288, i => 1); //524288 mod (32768) == 0
        byte itemTwo = cache.GetOrAdd(262144, i => 2); //262144 mod (32768) == 0

        Assert.That(itemOne,                  Is.EqualTo((byte)1));
        Assert.That(itemTwo,                  Is.EqualTo((byte)2));
        Assert.That(cache.RealCacheItemCount, Is.EqualTo(2));
    }
#if HAVE_PARALLELPERFORMANCE
    [Test]
    public void MultiThreadedOverwriteBucketTest()
    {                    
        FlashCacheWithBuckets<int, int> cache                  = new FlashCacheWithBuckets<int, int>(32768);
        Random                          random                 = new Random();
        int                             numberOfHashcodesToUse = 1024;
        bool                            shouldWait             = Environment.ProcessorCount > 1;

        //Generate values that would refer to the same index, but with different hashcodes
        int[] hashCodesThatWouldReferToSameIndexArray = new int[numberOfHashcodesToUse];

        int k = 0;
        for (int i = cache.MaxItemCount; k < numberOfHashcodesToUse; i = i + cache.MaxItemCount, k++)
        {
            if (i % cache.MaxItemCount == 0)
            {
                hashCodesThatWouldReferToSameIndexArray[k] = i;              
            }
        }

        //Add them multithreaded
        ParallelOptions parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = Environment.ProcessorCount};
        Parallel.For(0, numberOfHashcodesToUse,parallelOptions, i =>
                                                                {
                                                                    cache.GetOrAdd(hashCodesThatWouldReferToSameIndexArray[i], key =>
                                                                                                                               {
                                                                                                                                   if (shouldWait)
                                                                                                                                   {
                                                                                                                                       var dummy = random.Next(1, 5);
                                                                                                                                       dummy = random.Next(1, 5);
                                                                                                                                       //Force a little bit of duration
                                                                                                                                       //SpinWait.SpinUntil(() => false, random.Next(1, 5));                        
                                                                                                                                   }
                                                                                                                                   return key;
                                                                                                                               });
                                                                });

        //Assert        
        Parallel.For(0, numberOfHashcodesToUse, parallelOptions, i =>
                                                                 {
                                                                     int value;
                                                                     int key = hashCodesThatWouldReferToSameIndexArray[i];
                                                                     Assert.That(cache.TryGet(key, out value), Is.True, "Key was not found, key: "+ key);
                                                                     Assert.That(value, Is.EqualTo(key), "Key "+ key + " not equal to expected value: "+ value);
                                                                 });

        Assert.That(cache.CacheItemCount, Is.EqualTo(numberOfHashcodesToUse));
    }
#endif
    [Test]
    public void TrimShouldRemoveExcessValues()
    {
        FlashCacheWithBuckets<string, byte> cache = new FlashCacheWithBuckets<string, byte>(32768);

        for (int i = 0; i < cache.MaxItemCount * 2; i++)
        {
            cache.GetOrAdd(i.ToString(), key=> 1);
        }

        cache.TrimCache(cache.RealCacheItemCount - cache.MaxItemCount);

        Assert.That(cache.RealCacheItemCount, Is.EqualTo(cache.MaxItemCount));
    }

    [Test]
    public void TrimShouldRemoveZeroEntriesWhenCacheIsEmpty()
    {
        FlashCacheWithBuckets<string, byte> cache = new FlashCacheWithBuckets<string, byte>(32768);

        int count = cache.Trim(TrimOptions.Default);

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public void TrimWithDefaultShouldRemoveFivePercent()
    {
        FlashCacheWithBuckets<string, byte> cache = new FlashCacheWithBuckets<string, byte>(32768);
        for (int i = 0; i < cache.MaxItemCount * 2; i++)
        {
            cache.GetOrAdd(i.ToString(), key => 1);
        }
        int currentItemCount = cache.CacheItemCount;

        int trimCount = cache.Trim(TrimOptions.Default);

        Assert.That( (double) trimCount / currentItemCount, Is.EqualTo(0.05).Within(0.01));
    }

    [Test]
    public void TrimWithDefaultShouldRemoveTwo()
    {
        FlashCacheWithBuckets<string, byte> cache = new FlashCacheWithBuckets<string, byte>(32768);
        for (int i = 0; i < cache.MaxItemCount * 2; i++)
        {
            cache.GetOrAdd(i.ToString(), key => 1);
        }

        int trimCount = cache.Trim(TrimOptions.Cautious);

        Assert.That(trimCount, Is.EqualTo(2));
    }

    [Test]
    public void DisposeShouldNotThrowException()
    {
        FlashCacheWithBuckets<string, byte> cache = new FlashCacheWithBuckets<string, byte>(32768);

        Assert.That(()=> cache.Dispose(), Throws.Nothing);
    }

    protected override double MinimalEfficiencyInPercent { get { return 100; } }

    protected override IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int maxItemCount, IEqualityComparer<TKey> equalityComparer = null)
    {
        return new FlashCacheWithBuckets<TKey, TValue>(equalityComparer, maxItemCount);
    }
}