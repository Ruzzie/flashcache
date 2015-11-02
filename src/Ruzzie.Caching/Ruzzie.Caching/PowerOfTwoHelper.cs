using System;

namespace Ruzzie.Caching
{
    internal static class PowerOfTwoHelper
    {
        public static int FindNearestPowerOfTwoEqualOrLessThan(this int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot be negative.");
            }

            uint result = FindNearestPowerOfTwoEqualOrLessThan((uint) value);
            return Convert.ToInt32(result);
        }

        public static int FindNearestPowerOfTwoEqualOrGreaterThan(this int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Cannot be negative.");
            }

            const int maxSignedPowerOfTwo = 1073741824;
            uint result = FindNearestPowerOfTwoEqualOrGreaterThan((uint) value);

            if (result > maxSignedPowerOfTwo)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    "The value given would result in a value greater than 2^32 for a signed integer. Maximum value supported is " +
                    maxSignedPowerOfTwo);
            }
            return Convert.ToInt32(result);
        }

        private static uint FindNearestPowerOfTwoEqualOrGreaterThan(this uint value)
        {
            //http://stackoverflow.com/questions/5525122/c-sharp-math-question-smallest-power-of-2-bigger-than-x
            return PowTwoOf(value);
        }

        internal static uint FindNearestPowerOfTwoEqualOrLessThan(this uint value)
        {
            if (value == 2)
            {
                return 2;
            }

            value = value >> 1;
            value++;

            return PowTwoOf(value);
        }

        private static uint PowTwoOf(uint value)
        {
            uint x = value;
            x--; // comment out to always take the next biggest power of two, even if x is already a power of two
            x |= (x >> 1);
            x |= (x >> 2);
            x |= (x >> 4);
            x |= (x >> 8);
            x |= (x >> 16);
            uint result = (x + 1);
            return result;
        }

        public static bool IsPowerOfTwo(this uint x)
        {
            return ((x & (x - 1)) == 0);
        }
    }
}