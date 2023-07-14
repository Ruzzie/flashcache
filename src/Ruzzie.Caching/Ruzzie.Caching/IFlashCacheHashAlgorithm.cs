using System;

namespace Ruzzie.Caching;

/// <summary>
/// Interface for custom hash algoritms. These are not cryptographic hashes but for hashspreading alternative purposes.
/// </summary>
[Obsolete("This type will be removed")]
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

    /// <summary>
    /// Hashes the bytes.
    /// </summary>
    /// <param name="bytesToHash">The get bytes.</param>
    /// <returns></returns>
    /// <exception cref="ArgumentNullException">if the bytes are null</exception>
    long HashBytes64(byte[] bytesToHash);

    /// <summary>
    /// Hashes a string, case insensitive.
    /// </summary>
    /// <param name="stringToHash">The string to hash.</param>
    /// <returns>a hashcode</returns>
    long HashStringCaseInsensitive64(string stringToHash);
}