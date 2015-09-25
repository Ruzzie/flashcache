using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
   public class MemoryCacheWithSizeLimitTests
    {

        private static Stopwatch TimeSingleThreadCacheWrite(MemoryCacheWithSizeLimit<int> cache)
        {
            Stopwatch timer = new Stopwatch();

            timer.Start();
            for (var i = 0; i < 250000; i++)
            {
                if (i % 2 == 0)
                {
                    int i1 = i;
                    cache.GetOrAdd(i.ToString(), s => i1);
                }
                else
                {
                    int i1 = i;
                    cache.GetOrAdd((i - 5).ToString(), s => (i1 - 5));
                }
            }
            timer.Stop();
            return timer;
        }

        [Test]
        public void TryGetForValueTypeShouldReturnFalseForNonExistingKey()
        {
            MemoryCacheWithSizeLimit<int> cache = new MemoryCacheWithSizeLimit<int>(1);

            int value;
            Assert.That(cache.TryGet("NotExistingKey", out value), Is.False);            
        }

        [Test]
        public void TryGetForValueTypeShouldSetOutValue()
        {
            MemoryCacheWithSizeLimit<int> cache = new MemoryCacheWithSizeLimit<int>(1);
            cache.GetOrAdd("1", s => 99);

            int value;
            Assert.That(cache.TryGet("1", out value), Is.True);
            Assert.That(value, Is.EqualTo(99));
        }

        [Test]
        public void NullKeyTest()
        {
            MemoryCacheWithSizeLimit<string> cache = new MemoryCacheWithSizeLimit<string>(5);

            Assert.That(() => cache.GetOrAdd(null, s => null), Throws.TypeOf<ArgumentNullException>());
        }

        [Test]
        public void NullValuesTest()
        {
            MemoryCacheWithSizeLimit<string> cache = new MemoryCacheWithSizeLimit<string>(4);

            cache.GetOrAdd("MyKey0", s => null);
            cache.GetOrAdd("MyKey1", s => null);
            cache.GetOrAdd("MyKey2", s => null);
            cache.GetOrAdd("MyKey3", s => null);
            cache.GetOrAdd("MyKey4", s => null);
            cache.GetOrAdd("MyKey5", s => null);
            cache.GetOrAdd("MyKey6", s => null);
            cache.GetOrAdd("MyKey7", s => null);
            cache.GetOrAdd("MyKey8", s => null);
            cache.GetOrAdd("MyKey9", s => null);
            cache.GetOrAdd("MyKey10", s => null);
            cache.GetOrAdd("MyKey11", s => null);
            cache.GetOrAdd("MyKey12", s => null);
        }

        [Test]
        public void SingleThreadPerformanceTest()
        {
            MemoryCacheWithSizeLimit<int> cache = new MemoryCacheWithSizeLimit<int>(250000);
            ConcurrentBag<long> allTickTimes = new ConcurrentBag<long>();
            ConcurrentBag<long> allElapsedTimesMilliseconds = new ConcurrentBag<long>();

            for (var i = 0; i < 10; i++)
            {
                Stopwatch timer = TimeSingleThreadCacheWrite(cache);
                allTickTimes.Add(timer.ElapsedTicks);
                allElapsedTimesMilliseconds.Add(timer.ElapsedMilliseconds);
            }

            double averageTickTime = allTickTimes.Average();
            double averageTimeMilliseconds = allElapsedTimesMilliseconds.Average();

            Console.WriteLine("Average ticks: " + averageTickTime);
            Console.WriteLine("Average ms: " + averageTimeMilliseconds);

            //Assert.That(averageTickTime, Is.LessThanOrEqualTo(1033126));
            //Assert.That(averageTimeMilliseconds, Is.LessThanOrEqualTo(343));
        }

        [Test]
        public void Smokey()
        {
            MemoryCacheWithSizeLimit<string> cache = new MemoryCacheWithSizeLimit<string>(5);

            // ReSharper disable UnusedVariable
            string valueOne = cache.GetOrAdd("1", key => "1Value");
            
            string valueTwo = cache.GetOrAdd("2", key => "2Value");
            string valueThree = cache.GetOrAdd("3", key => "3Value");
            string valueFour = cache.GetOrAdd("4", key => "4Value");
            string valueFive = cache.GetOrAdd("5", key => "5Value");

            string valueSix = cache.GetOrAdd("6", key => "6Value");
            // ReSharper restore UnusedVariable

            Assert.That(cache.GetOrAdd("1", key => "1ValueUpdated"), Is.EqualTo("1Value").Or.EqualTo("1ValueUpdated")); //because of the delayed trim of the cache
            Assert.That(cache.GetOrAdd("6", key => "DoNotUpdate6"), Is.EqualTo("6Value").Or.EqualTo("DoNotUpdate6"));
            Assert.That(cache.GetOrAdd("5", key => "DoNotUpdate5"), Is.EqualTo("5Value").Or.EqualTo("DoNotUpdate5"));
            Assert.That(cache.GetOrAdd("4", key => "DoNotUpdate4"), Is.EqualTo("4Value").Or.EqualTo("DoNotUpdate4"));
            Assert.That(cache.GetOrAdd("3", key => "DoNotUpdate3"), Is.EqualTo("3Value").Or.EqualTo("DoNotUpdate3"));
            Assert.That(cache.GetOrAdd("2", key => "Update2"), Is.EqualTo("Update2").Or.EqualTo("2Value"));
        }
    }
}
