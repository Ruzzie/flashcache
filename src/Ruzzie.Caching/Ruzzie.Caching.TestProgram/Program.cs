using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Ruzzie.Caching.Tests;
using Ruzzie.Common;

namespace Ruzzie.Caching.TestProgram
{
    class Program
    {
        private static readonly Random Random = new SimpleRandom(Environment.TickCount);

        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        private static void Main()
        {
            FlashCacheWithBucketsTests tests = new FlashCacheWithBucketsTests();
            try
            {
                //Run a lot of times to see if rare race condition takes place
                //No exceptions should occurr and the tests should succeed.
                var numberOfRuns = 128;

                Console.WriteLine("\nMultiThreadedTest Run for 60 seconds.");

                Task testTask = Task.Run(() => tests.MultiThreadedTest(60 * 1000));

                Console.WriteLine("\nMultiThreadedOverwriteBucketTest number of runs: " + numberOfRuns);
                for (int i = 0; i < numberOfRuns; i++)
                {
                    tests.MultiThreadedOverwriteBucketTest();
                    Console.Write("\r  Count: " + i);
                }
               
                Console.WriteLine("\nMultiThreadedReadWriteTest number of runs: " + numberOfRuns);
                for (int i = 0; i < numberOfRuns; i++)
                {
                    tests.MultiThreadedReadWriteTest();
                    Console.Write("\r  Count: " + i);
                }

                testTask.Wait();
                GC.Collect();

          
                //Run a task where the performance should not degrade significantly over time
                IFixedSizeCache<string,Item> cache = new FlashCacheWithBuckets<string, Item>(512,new StringComparerOrdinalIgnoreCaseFNV1AHash());
                long startMemory = GC.GetTotalMemory(true);
                GC.KeepAlive(cache);
                Console.WriteLine("\nCache testing with MaxItemCount: "+cache.MaxItemCount);
                //prefill the cache for half
                for (int i = 0; i < cache.MaxItemCount / 2; i++)
                {
                    cache.GetOrAdd(i.ToString(), s => new Item());
                }

                bool mustRun = true;
                int numberOfWriteThreads = 8;
                double minAverage = 0;
                double maxAverage = 0;
                double averageDelta = 0;
                int sampleCount = 0;
                Task[] tasks = new Task[numberOfWriteThreads];

                Average average = new Average();

                for (int i = 0; i < 8; i++)
                {                    
                    tasks[i] = Task.Run(() => WriteToCacheLoop(ref mustRun, cache, average));
                }

                
                int sampleIntervalInMillis = 250;
                double previousAverage = 1000;
                Task sampleAverageTask = Task.Run(() =>
                {
                    Thread.Sleep(sampleIntervalInMillis);
                    while (mustRun)
                    {
                        Thread.Sleep(sampleIntervalInMillis);
                        double currentAverageLocal = average.CurrentAverage;

                        if (currentAverageLocal > maxAverage)
                        {
                            maxAverage = currentAverageLocal;
                        }

                        if (currentAverageLocal < minAverage)
                        {
                            minAverage = currentAverageLocal;
                        }

                        double difference = previousAverage - currentAverageLocal;
                        averageDelta = Average.StreamAverage(averageDelta, difference, sampleCount);

                        previousAverage = currentAverageLocal;
                        sampleCount++;

                        string cacheItemCount = cache.CacheItemCount.ToString("0000");
                        Console.Write("\r Avg: " + currentAverageLocal.ToString("F") + " ns." + " Avg delta: " + averageDelta.ToString("F") + " CacheItemCount: "+cacheItemCount+" SampleCount: "+sampleCount);
                        //trim the cache to increase lock contention
                        cache.Trim(TrimOptions.Aggressive);
                    }

                });
             
                Console.ReadKey();        
                    
                mustRun = false;
                sampleAverageTask.Wait();
                Task.WaitAll(tasks);

                cache.Trim(TrimOptions.Aggressive);

                long after = GC.GetTotalMemory(true);

                double diff = after - startMemory;

                Console.WriteLine("\nDone Cache testing with MaxItemCount: " + cache.MaxItemCount + " items in cache: " + cache.CacheItemCount + " estimated size of cache memory is: "+ (diff / 1024) / 1024+" Mb");

            }
            catch (Exception e)
            {
                Console.WriteLine("Failed with: "+e.Message);
                throw;
            }
        }

        static Item EmptyItem = new Item();

        private static void WriteToCacheLoop(ref bool mustLoop, IFixedSizeCache<string, Item> cache, Average average)
        {           
            Stopwatch timer = new Stopwatch();
            long counter = 0;
            int startAtCount = cache.MaxItemCount /2;
            while (mustLoop)
            {
                //byte[] charBuffer = new byte[40];
                //_random.NextBytes(charBuffer);
                //string key = Encoding.Unicode.GetString(charBuffer).ToLowerInvariant();                

                string key = (Guid.NewGuid().ToString()).PadLeft(Random.Next(40), (char)Random.Next(256));
                //Console.WriteLine(key);
                timer.Restart();
                // ReSharper disable once PossibleNullReferenceException      
                cache.GetOrAdd(key, k => EmptyItem);
                timer.Stop();
               
                if (counter > startAtCount)
                {
                    double averageCallTimeInNs = timer.Elapsed.Ticks*100;
                    average.Add(averageCallTimeInNs);
                }
                counter++;
            }
        }
    }

    internal class Average
    {
        private long _numberOfSamples;
        private double _currentAverage;

        readonly object _lockObject = new object();

        public Average(double initialValue =0)
        {
            _currentAverage = initialValue;
        }

        public double CurrentAverage
        {
            get { return _currentAverage; }           
        }

        public void Add(double averageCallTimeInNs)
        {
            lock (_lockObject)
            {
                _currentAverage = StreamAverage(CurrentAverage, averageCallTimeInNs, _numberOfSamples);
                _numberOfSamples++;
            }
        }

        public static double StreamAverage(double previousAverage, double currentNumber, double currentCount)
        {
            return ((previousAverage * currentCount) + currentNumber) / (currentCount + 1);
        }
    }

    internal class Item
    {
        public int HashCode { get; set; }
        public string Key { get; set; }
        public IEnumerable<int> Value { get; set; }
    }
}
