using Ruzzie.Common.Hashing;

namespace Ruzzie.Caching
{
    /// <summary>
    /// An implementation of the FNV-1a hash. http://www.isthe.com/chongo/tech/comp/fnv/
    /// </summary>
    public class FNV1AHashAlgorithmWrap : IFlashCacheHashAlgorithm
    {
        private static readonly FNV1AHashAlgorithm FNV1AHashAlgorithm = new FNV1AHashAlgorithm();
        private static readonly FNV1AHashAlgorithm64 FNV1AHashAlgorithm64 = new FNV1AHashAlgorithm64();

        /// <summary>
        /// Hashes the bytes.
        /// </summary>
        /// <param name="bytesToHash">The bytes to hash.</param>
        /// <returns>a 64 bit hashcode</returns>
        public long HashBytes64(byte[] bytesToHash)
        {
            return FNV1AHashAlgorithm64.HashBytes(bytesToHash);
        }

        /// <summary>
        /// Hashes a string, case insensitive.
        /// </summary>
        /// <param name="stringToHash">The string to hash.</param>
        /// <returns>
        /// a hashcode
        /// </returns>
        public long HashStringCaseInsensitive64(string stringToHash)
        {
            return FNV1AHashAlgorithm64.HashStringCaseInsensitive(stringToHash);
        }

        /// <summary>
        /// Hashes the bytes.
        /// </summary>
        /// <param name="bytesToHash">The bytes to hash.</param>
        /// <returns>
        /// a hashcode
        /// </returns>
        public int HashBytes(byte[] bytesToHash)
        {
            return FNV1AHashAlgorithm.HashBytes(bytesToHash);
        }

        /// <summary>
        /// Hashes a string, case insensitive.
        /// </summary>
        /// <param name="stringToHash">The string to hash.</param>
        /// <returns>
        /// a hashcode
        /// </returns>
        public int HashStringCaseInsensitive(string stringToHash)
        {
            return FNV1AHashAlgorithm.HashStringCaseInsensitive(stringToHash);
        }
    }
}