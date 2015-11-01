using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
    public class FlashCacheWithBucketsTests : FixedCacheBaseTests
    {
        [Test]
        public void SmokeTest()
        {
            FlashCacheWithBuckets<string,byte> cache = new FlashCacheWithBuckets<string,byte>(1, StringComparer.OrdinalIgnoreCase);

            Assert.That(cache.GetOrAdd("1",s => 1), Is.EqualTo((byte) 1));
            Assert.That(cache.CacheItemCount, Is.EqualTo(1));
        }

        [Test]
        public void ItemsWithSameIndexShouldStillBeStoredWhenEnoughCapacity()
        {
            FlashCacheWithBuckets<int, byte> cache = new FlashCacheWithBuckets<int, byte>(1);

            Debug.WriteLine(cache.MaxItemCount);

            cache.GetOrAdd(524288, i => (byte)1);         //524288 mod (32768) == 0
            byte itemTwo = cache.GetOrAdd(262144, i => 2);//262144 mod (32768) == 0

            Assert.That(cache.GetOrAdd(524288, i => 2), Is.EqualTo((byte) 1));
            Assert.That(itemTwo, Is.EqualTo((byte) 2));
            Assert.That(cache.RealCacheItemCount, Is.EqualTo(2));
        }

        [Test]
        public void ShouldOverwriteBucketWhenValueIsUpdated()
        {
            FlashCacheWithBuckets<int, byte> cache = new FlashCacheWithBuckets<int, byte>(1);

            Debug.WriteLine(cache.MaxItemCount);

            byte itemOne = cache.GetOrAdd(524288, i => 1);//524288 mod (32768) == 0
            byte itemTwo = cache.GetOrAdd(262144, i => 2);//262144 mod (32768) == 0

            Assert.That(itemOne, Is.EqualTo((byte)1));
            Assert.That(itemTwo, Is.EqualTo((byte)2));
            Assert.That(cache.RealCacheItemCount, Is.EqualTo(2));
        }

        [Test]
        public void MultiThreadedOverwriteBucketTest()
        {                    
            FlashCacheWithBuckets<int, int> cache = new FlashCacheWithBuckets<int, int>(1);
            Random random = new Random();
            int numberOfHashcodesToUse = 8192;

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
            ParallelOptions parallelOptions = new ParallelOptions {MaxDegreeOfParallelism = -1};
            Parallel.For(0, numberOfHashcodesToUse,parallelOptions, i =>
            {
                cache.GetOrAdd(hashCodesThatWouldReferToSameIndexArray[i], key =>
                {
                    //Force a little bit of duration
                    Thread.SpinWait(random.Next(64,1024));
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

        [Test]
        public void TrimShouldRemoveExcessValues()
        {
            FlashCacheWithBuckets<string, byte> cache = new FlashCacheWithBuckets<string, byte>(4);

            for (int i = 0; i < cache.MaxItemCount * 2; i++)
            {
                cache.GetOrAdd(i.ToString(), key=> 1);
            }

            cache.TrimCache(cache.RealCacheItemCount - cache.MaxItemCount);

            Assert.That(cache.RealCacheItemCount, Is.EqualTo(cache.MaxItemCount));
        }      

        public override IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int size)
        {
            return new FlashCacheWithBuckets<TKey, TValue>(size);
        }

        public override double MinimalEfficiencyInPercent { get { return 100; } }

        protected override IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int size, IEqualityComparer<TKey> ordinalIgnoreCase)
        {
            return new FlashCacheWithBuckets<TKey, TValue>(size,ordinalIgnoreCase);
        }
    }
}
