namespace Ruzzie.Cacing.Tests
{
    //http://www.dotnetperls.com/prime
    internal static class PrimeTool
    {
        public static bool IsPrime(int candidate)
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
            for (var i = 3; (i*i) <= candidate; i += 2)
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