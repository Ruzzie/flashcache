using System.Collections.Generic;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests;

[TestFixture]
public class FlashCacheTests : FixedCacheBaseTests
{
    protected override double MinimalEfficiencyInPercent { get { return 46; } }

    protected override IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int maxItemCount, IEqualityComparer<TKey> equalityComparer = null)
    {
        return new FlashCache<TKey, TValue>(equalityComparer, maxItemCount);
    }
              
    [Test]
    public void TestGetIndexKnuth()
    {
        int i = GetIndexKnuth(8, 16);
            
        Assert.That(i, Is.EqualTo(3));
    }

    private static int GetIndexKnuth(int hashCodeForKey, int maxItemCount, uint a = 0x678DDE6F )
    {
        uint n = (uint)maxItemCount;
        uint d = ( 0xFFFFFFFF            / n) + 1U;
        uint i = ((uint)(hashCodeForKey) * a) / d;

        return (int) i; // & (maxItemCount - 1);
    }
}