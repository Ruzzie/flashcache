namespace Ruzzie.Caching
{
    /// <summary>
    /// Interface for custom hash algoritms. These are not cryptographic hashes but for hashspreading alternative purposes.
    /// </summary>
    public interface IFlashCacheHashAlgorithm
    {
        /// <summary>
        /// Hashes the bytes.
        /// </summary>
        /// <param name="bytesToHash">The bytes t0 hash.</param>
        /// <returns>a hashcode</returns>
        int HashBytes(byte[] bytesToHash);
        /// <summary>
        /// Hashes a string, case insensitive.
        /// </summary>
        /// <param name="stringToHash">The string to hash.</param>
        /// <returns>a hashcode</returns>
        int HashStringCaseInsensitive(string stringToHash);
    }
}