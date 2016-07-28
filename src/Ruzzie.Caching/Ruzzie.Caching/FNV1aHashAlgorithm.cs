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
        /// <param name="bytesToHash">The get bytes.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">if the bytes are null</exception>
        public int HashBytes(byte[] bytesToHash)
        {
            if (ReferenceEquals(bytesToHash,null))
            {
                throw new ArgumentNullException(nameof(bytesToHash));
            }

            return HashBytesInternal(bytesToHash);
        }

        /// <summary>
        /// Hashes the string case insensitive.
        /// </summary>
        /// <param name="stringToHash">The string to hash.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">if the string is null</exception>
        public int HashStringCaseInsensitive(string stringToHash)
        {
            if (ReferenceEquals(stringToHash,null))
            {
                throw new ArgumentNullException(nameof(stringToHash));
            }
            return GetInvariantCaseInsensitiveHashCode(stringToHash);
        }

#if !PORTABLE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static int HashBytesInternal(byte[] bytesToHash)
        {
            uint hash = FNVOffsetBasis32;
            int byteCount = bytesToHash.Length;

            for (int i = 0; i < byteCount; ++i)
            {
                hash = HashByte(hash, bytesToHash[i]);
            }
            return (int) hash;
        }

#if !PORTABLE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static int GetInvariantCaseInsensitiveHashCode(string stringToHash)
        {
            uint hash = FNVOffsetBasis32;
            int stringLength = stringToHash.Length;

            for (int i = 0; i < stringLength; ++i)
            {
                ushort currChar = InvariantTextInfo.ToUpper(stringToHash[i]);
                byte byteOne = (byte) currChar; //lower bytes              
                byte byteTwo = (byte) (currChar >> 8); //uppper byts
                
                hash = HashByte(HashByte(hash, byteOne), byteTwo);
            }
            return (int) hash;
        }

#if ! PORTABLE
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
#endif
        private static uint HashByte(uint currentHash, byte byteToHash)
        {
           return (currentHash ^ byteToHash) * FNVPrime32;
        }      
    }
}