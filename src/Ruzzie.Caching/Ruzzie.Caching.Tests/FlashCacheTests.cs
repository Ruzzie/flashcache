using System;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
    public class FlashCacheTests
    {
        

        private class KeyTestTypeWithConstantHash
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

        [Test]
        public void GetOrAddThrowsArgumentNullExceptionWhenKeyIsNull()
        {
            Assert.That(()=>new FlashCache<string,string>(1).GetOrAdd("1",null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void GetOrAddShouldCacheItem()
        {
            FlashCache<string, int> cache = new FlashCache<string, int>(1);
            var numberOfTimesCalled = 0;

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

        [TestCase(1,1)]
        [TestCase(2,2)]
        [TestCase(10,10)]
        [TestCase(1024,1024)]
        public void CacheItemCountShouldReturnOnlyItemsInCache(int numberOfItemsToInsert, int expectedCount)
        {
            FlashCache<string,Guid> cache = new FlashCache<string, Guid>(1);

            for (int i = 0; i < numberOfItemsToInsert; i++)
            {
                cache.GetOrAdd(i.ToString(), key => Guid.NewGuid());
            }
            
            Assert.That(cache.CacheItemCount, Is.EqualTo(expectedCount));
        }

        [Test]
        public void GetOrAddShouldReturnValueFactoryResult()
        {
            FlashCache<string, int> cache = new FlashCache<string, int>(1);

            int value = cache.GetOrAdd("key", key => { return 1; });

            Assert.That(value, Is.EqualTo(1));
        }

        [Test]
        public void InitializeWithSizeLessThanOneShouldThrowArgumentException()
        {
            Assert.That(() => new FlashCache<int, int>(0), Throws.ArgumentException);
        }

        [Test]
        public void OverwriteCacheLocationWithSameHash()
        {
            FlashCache<KeyTestTypeWithConstantHash, int> flashCache = new FlashCache<KeyTestTypeWithConstantHash, int>(1);

            flashCache.GetOrAdd(new KeyTestTypeWithConstantHash {Value = "10"}, i => 10);
            int value = flashCache.GetOrAdd(new KeyTestTypeWithConstantHash {Value = "99"}, i => 99);

            Assert.That(value, Is.EqualTo(99));
        }

        [Test]
        public void ShouldCacheSameValue()
        {
            FlashCache<string, int> flashCache = new FlashCache<string, int>(1);

            flashCache.GetOrAdd("10", i => 10);
            int value = flashCache.GetOrAdd("10", i => 99);

            Assert.That(value, Is.EqualTo(10));
        }

        private readonly FlashCache<int, int> _flashCacheShouldCacheSameValues = new FlashCache<int, int>(1);
        [Test]
        [TestCase(0,0,0)]
        [TestCase(1,1,1)]
        [TestCase(1024,1024,1024)]
        [TestCase(523, 523, 523)]
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
            new FlashCache<string, string>(10, StringComparer.OrdinalIgnoreCase);
        }

        [Test]
        public void ShouldInitializeWithFixedMaximumSizeToNearestPowerOfTwo()
        {
            FlashCache<string, string> cache = new FlashCache<string, string>(1024);

            Assert.That(cache.MaxItemCount, Is.EqualTo(134217728));
        }

        [Test]
        public void ShouldInitializeWithPassedSizesForReferenceType()
        {
            FlashCache<string, string> cache = new FlashCache<string, string>(4,averageSizeInBytesOfKey:48, averageSizeInBytesOfValue:48);

            Assert.That(cache.MaxItemCount, Is.EqualTo(40329));
        }

        [TestCase(1,1)]
        [TestCase(20,20)]
        [TestCase(100,100)]
        [TestCase(5212,1024)]
        public void MaxMbShouldBeLessOrEqualToGivenMaxMb(int givenMax, int expected)
        {
            FlashCache<string, string> cache = new FlashCache<string, string>(givenMax);

            Assert.That(cache.SizeInMb, Is.EqualTo(expected).And.LessThanOrEqualTo(givenMax));
        }
      

        [Test]
        public void ShouldInitializeWithFixedMaximumSizeToNearestPowerOfTwoWhenMaxIntIsGiven()
        {
            FlashCache<string, string> cache = new FlashCache<string, string>((int.MaxValue)/2);

            Assert.That(cache.MaxItemCount, Is.EqualTo(134217728));
        }

        [Test]
        public void InsertTwoItemsTest()
        {
            var keyOne = "No hammertime for: 19910";
            var keyTwo = "No hammertime for: 90063";

            var cache = new FlashCache<string, string>(13);

            Assert.That(cache.GetOrAdd(keyOne, s => keyOne), Is.EqualTo(keyOne));
            Assert.That(cache.GetOrAdd(keyTwo, s => keyTwo), Is.EqualTo(keyTwo));
        }

        [Test]
        public void TryGetShouldReturnTrueWhenValueIsInCache()
        {
            FlashCache<string,int> cache = new FlashCache<string, int>(1);

            cache.GetOrAdd("1", s => 1);
            int value;
            Assert.That(cache.TryGet("1",out value), Is.True);
        }

        [Test]
        public void TryGetShouldSetOutValueWhenValueIsInCache()
        {
            FlashCache<string, int> cache = new FlashCache<string, int>(1);

            cache.GetOrAdd("1", s => 1);
            int value;
            cache.TryGet("1", out value);
            Assert.That(value, Is.EqualTo(1));
        }

        [Test]
        public void TryGetShouldReturnFalseWhenValueIsNotInCache()
        {
            FlashCache<string, int> cache = new FlashCache<string, int>(1);
          
            int value;
            Assert.That(cache.TryGet("1", out value), Is.False);
        }

        [Test]
        public void TryGetShouldSetOutValueToDefaultWhenValueIsNotInCache()
        {
            FlashCache<string, int> cache = new FlashCache<string, int>(1);

            int value;
            cache.TryGet("1", out value);
            Assert.That(value, Is.EqualTo(0));
        }

        [Test]
        public void TryGetShouldReturnFalseForItemWithSameHashcodeAndDifferentValues()
        {
            FlashCache<KeyTestTypeWithConstantHash, string> cache = new FlashCache<KeyTestTypeWithConstantHash, string>(1);
            cache.GetOrAdd(new KeyTestTypeWithConstantHash {Value = "a"}, hash => "1");

            string value;
            Assert.That(cache.TryGet(new KeyTestTypeWithConstantHash {Value = "b"}, out value), Is.False);
        }
    }
}