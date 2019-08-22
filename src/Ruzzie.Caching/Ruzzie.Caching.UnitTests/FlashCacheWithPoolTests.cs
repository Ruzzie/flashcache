using System.Collections.Generic;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests
{
    [TestFixture]
    public class FlashCacheWithPoolTests : FixedCacheBaseTests
    {
      
        protected override double MinimalEfficiencyInPercent { get { return 46; } }

        protected override IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int size, IEqualityComparer<TKey> equalityComparer = null)
        {
            return new FlashCacheWithPool<TKey, TValue>(size, equalityComparer);
        }
        

        [Test]
        public void ShouldInitializeWithPassedSizesForReferenceType()
        {
            var cache = new FlashCacheWithPool<string, string>(4,averageSizeInBytesOfKey:48, averageSizeInBytesOfValue:48);

            Assert.That(cache.MaxItemCount, Is.EqualTo(8192));
        }
    }
}