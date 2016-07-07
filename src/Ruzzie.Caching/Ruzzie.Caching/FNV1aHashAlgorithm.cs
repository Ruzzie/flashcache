using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Ruzzie.Caching
{
    /// <summary>
    /// An implementation of the FNV-1a hash. http://www.isthe.com/chongo/tech/comp/fnv/
    /// </summary>
    public class FNV1AHashAlgorithm : IFlashCacheHashAlgorithm
    {
        private static readonly TextInfo InvariantTextInfo = CultureInfo.InvariantCulture.TextInfo;
        const uint FNVPrime32 = 16777619;
        const uint FNVOffsetBasis32 = 2166136261;

        /// <summary>
        /// Hashes the bytes.
        /// </summary>
        /// <param name="getBytes">The get bytes.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">if the bytes are null</exception>
        public int HashBytes(byte[] getBytes)
        {
            if (getBytes == null)
            {
                throw new ArgumentNullException(nameof(getBytes));
            }

            return HashBytesInternal(getBytes);
        }

        /// <summary>
        /// Hashes the string case insensitive.
        /// </summary>
        /// <param name="stringToHash">The string to hash.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">if the string is null</exception>
        public int HashStringCaseInsensitive(string stringToHash)
        {
            if (stringToHash == null)
            {
                throw new ArgumentNullException(nameof(stringToHash));
            }
            return GetInvariantCaseInsensitiveHashCode(stringToHash);
        }

#if !PORTABLE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static int HashBytesInternal(byte[] getBytes)
        {
          
            uint hash = FNVOffsetBasis32;

            for (int i = 0; i < getBytes.Length; ++i)
            {
                hash = HashByte(hash, getBytes[i]);
            }
            return (int) hash;
        }

#if !PORTABLE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static int GetInvariantCaseInsensitiveHashCode(string stringToHash)
        {
            uint hash = FNVOffsetBasis32;
            for (int i = 0; i < stringToHash.Length; ++i)
            {
                ushort currChar = InvariantTextInfo.ToUpper(stringToHash[i]);
                byte byteOne = (byte) currChar;              
                byte byteTwo = (byte) (currChar >> 8);

                hash = HashByte(hash, byteOne);
                hash = HashByte(hash, byteTwo);
            }
            return (int) hash;
        }

#if ! PORTABLE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static uint HashByte(uint currentHash, byte byteToHash)
        {
            currentHash = currentHash ^ byteToHash;
            currentHash = currentHash * FNVPrime32;
            return currentHash;
        }      
    }
}