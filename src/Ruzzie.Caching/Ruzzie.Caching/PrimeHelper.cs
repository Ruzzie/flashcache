﻿using System;

namespace Ruzzie.Caching
{
    //http://www.dotnetperls.com/prime
    internal static class PrimeHelper
    {
        private static readonly long[] Primes;

        static PrimeHelper()
        {
            //
            // Initialize array of first primes before methods are called.
            //
            Primes = new long[]
            {
                3, 7, 11, 17, 23, 29, 37,
                47, 59, 71, 89, 107, 131,
                163, 197, 239, 293, 353,
                431, 521, 631, 761, 919,
                1103, 1327, 1597, 1931,
                2333, 2801, 3371, 4049,
                4861, 5839, 7013, 8419,
                10103, 12143, 14591, 17519,
                21023, 25229, 30293, 36353,
                43627, 52361, 62851, 75431,
                90523, 108631, 130363,
                156437, 187751, 225307,
                270371, 324449, 389357,
                467237, 560689, 672827,
                807403, 968897, 1162687,
                1395263, 1674319, 2009191,
                2411033, 2893249, 3471899,
                4166287, 4999559, 5999471,
                7199369
            };
        }

        public static long GetPrime(long min)
        {
            //
            // Get the first hashtable prime number
            // ... that is equal to or greater than the parameter.
            //
            for (long i = 0; i < Primes.Length; i++)
            {
                long num2 = Primes[i];
                if (num2 >= min)
                {
                    return num2;
                }
            }

            for (long j = min | 1; j < Int64.MaxValue -2/*2147483647*/; j += 2)
            {
                if (IsPrime(j))
                {
                    return j;
                }
            }
            return min;
        }

        public static bool IsPrime(long candidate)
        {
            // Test whether the parameter is a prime number.
            if ((candidate & 1) == 0)
            {
                if (candidate == 2)
                {
                    return true;
                }
                return false;
            }
            // Note:
            // ... This version was changed to test the square.
            // ... Original version tested against the square root.
            // ... Also we exclude 1 at the end.
            for (long i = 3; (i*i) <= candidate; i += 2)
            {
                if ((candidate%i) == 0)
                {
                    return false;
                }
            }
            return candidate != 1;
        }
    }
}