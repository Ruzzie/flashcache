using System.Collections.Generic;

namespace Ruzzie.Caching.UnitTests
{
    public static class Histogram
    {
        public static SortedDictionary<int, int> ToHistogram(this IEnumerable<int> nums)
        {
            var dict = new SortedDictionary<int, int>();
            foreach (var n in nums)
            {
                if (!dict.ContainsKey(n))
                    dict[n] = 1;
                else
                    dict[n] += 1;
            }
            return dict;
        }     
    }
}