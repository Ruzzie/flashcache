﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
    public class MultiThreadedCacheTests
    {
        [Test]
        public void FlashCacheMultiThreadedTest()
        {
            MultiThreadPerformanceTest(new FlashCache<string, int>(4, StringComparer.OrdinalIgnoreCase,8), 250000);
        }

        private void MultiThreadPerformanceTest(IMemoryCacheWithLimit<string, int> cache, int loopCount)
        {
            ConcurrentBag<long> allTickTimes = new ConcurrentBag<long>();
            ConcurrentBag<long> allElapsedTimesMilliseconds = new ConcurrentBag<long>();

            Parallel.For(0, 25, i =>
            {
                Task.Run(() => TimeParralelCacheWrite(cache, loopCount));
                Stopwatch timer = TimeParralelCacheWrite(cache, loopCount);

                allTickTimes.Add(timer.ElapsedTicks);
                allElapsedTimesMilliseconds.Add(timer.ElapsedMilliseconds);
            });

            double averageTickTime = allTickTimes.Average();
            double averageTimeMilliseconds = allElapsedTimesMilliseconds.Average();

            Console.WriteLine("Average ticks: " + averageTickTime);
            Console.WriteLine("Average ms: " + averageTimeMilliseconds);

            //Assert.That(averageTickTime, Is.LessThanOrEqualTo(3005055));
            //Assert.That(averageTimeMilliseconds, Is.LessThanOrEqualTo(1826)); //15298 spinlock || 1826 normal lock || 800 - 1300 MemoryCache || 300 flashcache

            Assert.That(cache.CacheItemCount, Is.LessThanOrEqualTo(300000));
        }

        private static Stopwatch TimeParralelCacheWrite(IMemoryCacheWithLimit<string,int> cache, int loopCount)
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
}
