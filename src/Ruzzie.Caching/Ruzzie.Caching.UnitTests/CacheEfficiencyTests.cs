using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests;

[SuppressMessage("ReSharper", "RedundantTypeArgumentsOfMethod")]
[Category("CacheEfficiencyTests")]
public abstract class CacheEfficiencyTests
{
    protected abstract IFixedSizeCache<TKey, TValue> CreateCache<TKey, TValue>(int maxItemCount, IEqualityComparer<TKey> comparer = null);

    protected abstract double MinimalEfficiencyInPercent { get; }

    [Test]
    public void BooleanEfficiency()
    {
        CacheEfficiencyShouldBe<bool>(i => i %2 == 0, 2);
    }

    [Test]
    public void ByteEfficiency()
    {
        CacheEfficiencyShouldBe<byte>(i => (byte)(i - (byte.MaxValue / 2)), 256);
    }

    [Test]
    public void SByteEfficiency()
    {
        CacheEfficiencyShouldBe<sbyte>(i => (sbyte)(i - (sbyte.MaxValue / 2)), 256);
    }

    [Test]
    public void Int16Efficiency()
    {
        CacheEfficiencyShouldBe<short>(i => (short)(i - (short.MaxValue / 2)));
    }

    [Test]
    public void UInt16Efficiency()
    {
        CacheEfficiencyShouldBe<ushort>(i => (ushort)(i - (ushort.MaxValue / 2)));
    }

    [Test]
    public void Int32Efficiency()
    {
        CacheEfficiencyShouldBe<int>(i => i - (int.MaxValue / 2));
    }

    [Test]
    public void UInt32Efficiency()
    {
        CacheEfficiencyShouldBe<uint>(i => (uint)i - (uint.MaxValue / 2));
    }

    [Test]
    public void Int64Efficiency()
    {
        CacheEfficiencyShouldBe<long>(i => i - (long.MaxValue / 2));
    }

    [Test]
    public void UInt64Efficiency()
    {
        CacheEfficiencyShouldBe<ulong>(i => (ulong)i - (ulong.MaxValue / 2));
    }

    [Test]
    public void FloatEfficiency()
    {
        CacheEfficiencyShouldBe<float>(i => (float)i / 2.0F * 10000.0F);
    }

    [Test]
    public void DoubleEfficiency()
    {
        CacheEfficiencyShouldBe<double>(i => (double)i / 2.0 * 10000.0);
    }

    [Test]
    public void DecimalEfficiency()
    {
        CacheEfficiencyShouldBe<decimal>(i => (decimal)i / 2.0m/* * 10000.0m*/);
    }

    [Test]
    public void CharEfficiency()
    {
        CacheEfficiencyShouldBe<char>(i => (char)(i - (char.MaxValue / 2)));
    }

    [Test]
    public void DateEfficiency()
    {
        CacheEfficiencyShouldBe<DateTime>(
                                          i => new DateTime((i % 9998) + 1, (i % 11) + 1, (i % 27) + 1, (i % 23) + 1, (i % 59) + 1, (i % 59) + 1));
    }

    [Test]
    public void StringEfficiency()
    {
        CacheEfficiencyShouldBe<string>(i => i.ToString().PadLeft(20, '0'), comparer: new StringComparerOrdinalIgnoreCaseFNV1AHash());
    }

    [Test]
    public void GuidEfficiency()
    {
        CacheEfficiencyShouldBe<Guid>(i => Guid.NewGuid());
    }

    [Test]
    public void TimespanEfficiency()
    {
        CacheEfficiencyShouldBe<TimeSpan>(i => TimeSpan.FromSeconds(i + i * i));
    }

    public void CacheEfficiencyShouldBe<T>(Func<int, T> keyFactory, int? customCacheItemCountToAssert = null, IEqualityComparer<T> comparer = null)
    {
        IFixedSizeCache<T, byte> cache                 = CreateCache<T, byte>(32768, comparer);
        int                      numberOfItemsToInsert = cache.MaxItemCount;
        for (int i = 0; i < numberOfItemsToInsert; i++)
        {
            cache.GetOrAdd(keyFactory.Invoke(i), key => 1);
        }

        double efficiency = ((double)cache.CacheItemCount / cache.MaxItemCount) * 100.0;

        Console.WriteLine("Cache efficiency is: " +efficiency);
        if (customCacheItemCountToAssert != null)
        {
            Assert.That(cache.CacheItemCount, Is.EqualTo(customCacheItemCountToAssert),
                        "Cache efficiency is not 100 percent for type: " + typeof(T));
        }
        else
        {
            Assert.That(efficiency, Is.GreaterThanOrEqualTo(MinimalEfficiencyInPercent), "Cache efficiency is not " + MinimalEfficiencyInPercent + " percent for type: " + typeof(T));
        }
    }
}