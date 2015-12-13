using System;
using Ruzzie.Common.Numerics;

namespace Ruzzie.Caching
{
    internal static class SizeHelper
    {
        internal static int RoundUpToNearest(this int number, int multiple)
        {
            if (number < 0)
            {
                throw new ArgumentOutOfRangeException("number","Only values greater than 0 are allowed.");
            }

            if (multiple <= 1)
            {
                throw new ArgumentOutOfRangeException("multiple", "Only values greater than 1 are allowed.");
            }

            if (number == 0)
            {
                return multiple;
            }

            return ((number - 1) | multiple - 1) + 1;
        }

        internal static int CalculateActualSizeInBytesForType(int totalSizeInBytesOfItemsInType, bool is64Bit, bool isValueType  = false)
        {
            int typeSize = isValueType ? 0: TypeHelper.TypeOverhead(is64Bit);

            if (totalSizeInBytesOfItemsInType == 0)
            {
                return typeSize;
            }

            int ptrSize = is64Bit ? 8 : 4;

            int actualSize = ((typeSize + totalSizeInBytesOfItemsInType) - (ptrSize)).RoundUpToNearest(ptrSize);

            return Math.Max(actualSize, typeSize);
        }

        internal static int CalculateMaxItemCountInPowerOfTwo(int maximumSizeInMb, int itemSizeInBytes)
        {
            long probableMaxArrayLength = (Constants.TwoGbInBytes - TypeHelper.ArrayOverHeadReferenceType()) / (itemSizeInBytes);

            long desiredArrayLength = ((maximumSizeInMb * (1024L) * (1024L)) / itemSizeInBytes);

            if (desiredArrayLength > probableMaxArrayLength)
            {
                return Convert.ToInt32(probableMaxArrayLength).FindNearestPowerOfTwoEqualOrLessThan();
            }

            return CalculateSizeWithMinimumOfOneMb(itemSizeInBytes, desiredArrayLength);
        }

        private static int CalculateSizeWithMinimumOfOneMb(int entryTypeSizeInBytes, long desiredArrayLength)
        {
            int minimumItemCount = (int) (Constants.OneMbInBytes/entryTypeSizeInBytes);
            int sizeBasedOnDesiredLength = Convert.ToInt32(desiredArrayLength + 2).FindNearestPowerOfTwoEqualOrLessThan();
            int sizeBasedOnMinimumCount = minimumItemCount.FindNearestPowerOfTwoEqualOrGreaterThan();
            return Math.Max(sizeBasedOnDesiredLength, sizeBasedOnMinimumCount);
        }
    }
}
