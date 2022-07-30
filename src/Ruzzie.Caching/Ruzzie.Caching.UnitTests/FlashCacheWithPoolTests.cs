using System.Collections.Generic;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests;

[TestFixture]
public class FlashCacheWithPoolTests : FixedCacheBaseTests
{
      
    protected override double MinimalEfficiencyInPercent { get { return 46; } }

    protected override IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int maxItemCount, IEqualityComparer<TKey> equalityComparer = null)
    {
        return new FlashCacheWithPool<TKey, TValue>(equalityComparer, maxItemCount);
    }
        
}