using System;

namespace Ruzzie.Caching
{
    internal static class SizeHelpers
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

            return ((number - 1) | multiple-1) + 1;
        }

        internal static int ActualSize(int totalSizeInBytesOfItemsInType, bool is64Bit, bool isValueType  = false)
        {
            int typeSize = isValueType ? 0: TypeHelper.TypeOverhead(is64Bit);

            if (totalSizeInBytesOfItemsInType == 0)
            {
                return typeSize;
            }

            var ptrSize = is64Bit ? 8 : 4;

            int actualSize = ((typeSize + totalSizeInBytesOfItemsInType) - (ptrSize)).RoundUpToNearest(ptrSize);

            return Math.Max(actualSize, typeSize);
        }

        internal static int CalculateMaxItemCountInPowerOfTwo(int maximumSizeInMb, int entryTypeSize)
        {
            long probableMaxArrayLength = (Constants.TwoGbInBytes - TypeHelper.ArrayOverHeadReferenceType()) / (entryTypeSize);

            long desiredArrayLength = ((maximumSizeInMb * (1024L) * (1024L)) / entryTypeSize);

            if (desiredArrayLength > probableMaxArrayLength)
            {
                return Convert.ToInt32(probableMaxArrayLength).FindNearestPowerOfTwoLessThan();
            }
            else
            {
                int minumumItemCount = (int)((1 * 1024L * 1024L) / entryTypeSize);
                int sizeBasedOnDesiredLength = Convert.ToInt32(desiredArrayLength + 2).FindNearestPowerOfTwoLessThan();
                int sizeBasedOnMinumumCount = minumumItemCount.FindNearestPowerOfTwo();
                return Math.Max(sizeBasedOnDesiredLength, sizeBasedOnMinumumCount);
            }
        }
    }
}
