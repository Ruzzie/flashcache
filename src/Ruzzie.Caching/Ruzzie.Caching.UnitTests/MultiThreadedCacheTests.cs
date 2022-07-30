using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ruzzie.Caching.UnitTests;
#if HAVE_PARALLELPERFORMANCE
[TestFixture,/* Ignore("Takes too long on CI.")*/]
public class MultiThreadedCacheTests
{
    [Test]
    public void FlashCacheWithPoolMultipleReadersAndWritersOfRefTypes_ShouldNotCollide()
    {
        var maxItemCount = 256;
        var cache = new FlashCacheWithPool<string, int[]>(
                                                          new StringComparerOrdinalIgnoreCaseFNV1AHash(),
                                                          maxItemCount);

        MultipleConcurrentReadersAndWritersWithRefValue_SmokeTest(cache, maxItemCount);
    }

    [Test]
    public void FlashCacheMultipleReadersAndWritersOfRefTypes_ShouldNotCollide()
    {
        var maxItemCount = 256;
        var cache = new FlashCache<string, int[]>(
                                                  new StringComparerOrdinalIgnoreCaseFNV1AHash(),
                                                  maxItemCount);

        MultipleConcurrentReadersAndWritersWithRefValue_SmokeTest(cache, maxItemCount);
    }

    [Test]
    public void FlashCacheWithBucketsMultipleReadersAndWritersOfRefTypes_ShouldNotCollide()
    {
        var maxItemCount = 256;
        var cache = new FlashCacheWithBuckets<string, int[]>(
                                                             new StringComparerOrdinalIgnoreCaseFNV1AHash(),
                                                             maxItemCount);

        MultipleConcurrentReadersAndWritersWithRefValue_SmokeTest(cache, maxItemCount);
    }

    private static void MultipleConcurrentReadersAndWritersWithRefValue_SmokeTest(
        IFixedSizeCache<string, int[]> cache,
        int                            maxItemCount)
    {
        Parallel.For(0, maxItemCount * 16, i =>
                                           {
                                               //We are going to force collisions
                                               var key        = (i % maxItemCount).ToString().PadLeft(10, '0');
                                               var firstValue = cache.GetOrAdd(key, k => new int[16]);

                                               Assert.That(firstValue, Is.Not.Null, $"firstValue is null key:[{key}]");

                                               var secondValue = cache.GetOrAdd(key, k => new int[16]);
                                               //if (secondValue == null)
                                               //{
                                               //    secondValue = cache.GetOrAdd(key, k => new int[16]);
                                               //}
                                               Assert.That(secondValue, Is.Not.Null, $"secondValue is null key:[{key}]");
                                           });
    }

        
        
    [Test]
    public void FlashCacheWithPoolMultiThreadedTest()
    {
        MultiThreadPerformanceTest(new FlashCacheWithPool<string, int>(StringComparer.OrdinalIgnoreCase, 131072), 50000);
    }

    [Test]
    public void FlashCacheMultiThreadedTest()
    {
        MultiThreadPerformanceTest(new FlashCache<string, int>(StringComparer.OrdinalIgnoreCase, 131072), 50000);
    }

    [Test]
    public void FlashCacheWithFNVHashMultiThreadedTest()
    {
        MultiThreadPerformanceTest(new FlashCache<string, int>(new StringComparerOrdinalIgnoreCaseFNV1AHash(), 131072), 50000);
    }

    [Test]
    public void FlashCacheWithBucketsMultiThreadedTest()
    {
        MultiThreadPerformanceTest(new FlashCacheWithBuckets<string, int>(StringComparer.OrdinalIgnoreCase, 131072), 50000);
    }

    [Test]
    public void FlashCacheWithBucketsWithFNVHashMultiThreadedTest()
    {
        MultiThreadPerformanceTest(new FlashCacheWithBuckets<string, int>(new StringComparerOrdinalIgnoreCaseFNV1AHash(), 131072), 50000);
    }

    private static void MultiThreadPerformanceTest(IFixedSizeCache<string, int> cache, int loopCount)
    {
        ConcurrentBag<long> allTickTimes                = new ConcurrentBag<long>();
        ConcurrentBag<long> allElapsedTimesMilliseconds = new ConcurrentBag<long>();

        Parallel.For(0, 25, i =>
                            {
                                Task.Run(() => TimeParralelCacheWrite(cache, loopCount));
                                Stopwatch timer = TimeParralelCacheWrite(cache, loopCount);

                                allTickTimes.Add(timer.ElapsedTicks);
                                allElapsedTimesMilliseconds.Add(timer.ElapsedMilliseconds);
                            });

        double averageTickTime         = allTickTimes.Average();
        double averageTimeMilliseconds = allElapsedTimesMilliseconds.Average();

        Console.WriteLine("Average ticks: " + averageTickTime);
        Console.WriteLine("Average ms: "    + averageTimeMilliseconds);

        //Assert.That(averageTickTime, Is.LessThanOrEqualTo(3005055));
        //Assert.That(averageTimeMilliseconds, Is.LessThanOrEqualTo(1826)); //spinlock > normal lock > MemoryCache > flashcaches

        Assert.That(cache.CacheItemCount, Is.LessThanOrEqualTo(300000));
    }

    private static Stopwatch TimeParralelCacheWrite(IFixedSizeCache<string,int> cache, int loopCount)
    {
        var hammertime = true;

        Task hammerOnCacheTask = Task.Run(() =>
                                          {
       
                                              // ReSharper disable AccessToModifiedClosure
                                              while (hammertime)
                    
                                              {
                                                  for (var i = 0; i < loopCount; i++)
                                                  {
                       
                                                      if (hammertime == false)
                                                      {
                                                          break;
                                                      }

                                                      if (i % 2 == 0)
                                                      {
                                                          int i1 = i;
                                                          cache.GetOrAdd(i1.ToString(), s => i1);                          
                                                      }
                                                      else
                                                      {
                                                          int i1 = i;
                                                          cache.GetOrAdd((i1 - 5).ToString(), s => (i1 - 5));
                                                      }
                                                  }
                                              }
                                          });
        // ReSharper restore AccessToModifiedClosure

        Stopwatch timer = new Stopwatch();

        timer.Start();
        Parallel.For(0, loopCount, i => { cache.GetOrAdd(i.ToString(), s => i); });

        timer.Stop();
        hammertime = false;
        hammerOnCacheTask.Wait(10);
        return timer;
    }
}
#endif