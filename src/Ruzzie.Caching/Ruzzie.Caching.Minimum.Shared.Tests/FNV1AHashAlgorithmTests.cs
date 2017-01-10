using System.Text;
using NUnit.Framework;

namespace Ruzzie.Caching.Minimum.Shared.Tests
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class FNV1aHashAlgorithmTests
    {
        private readonly IFlashCacheHashAlgorithm _hashAlgorithm = new FNV1AHashAlgorithmWrap();

        [Test]
        public void HashCodeTest()
        {
            Assert.That(_hashAlgorithm.HashStringCaseInsensitive("0"), Is.EqualTo(837651325));
        }

        [Test]
        public void SameHashCodeForSameString()
        {
            string stringToHash = "FlashCache is tha bomb";
            int hashOne = _hashAlgorithm.HashBytes(Encoding.Unicode.GetBytes(stringToHash));
            int hashTwo = _hashAlgorithm.HashBytes(Encoding.Unicode.GetBytes("FlashCache is tha bomb"));
            Assert.That(hashOne,Is.EqualTo(hashTwo));
        }

        [Test]
        public void DifferentHashCodeForDifferentString()
        {
            string stringToHash = "FlashCache is tha bomb you know";
            int hashOne = _hashAlgorithm.HashBytes(Encoding.Unicode.GetBytes(stringToHash));
            int hashTwo = _hashAlgorithm.HashBytes(Encoding.Unicode.GetBytes(stringToHash.ToLower()));

            Assert.That(hashOne, Is.Not.EqualTo(hashTwo));
        }

        [TestFixture]
        public class HashStringCaseInsensitive
        {
            private readonly IFlashCacheHashAlgorithm _hashAlgorithm = new FNV1AHashAlgorithmWrap();

            [TestCase("The Doctor", "the doctor")]
            [TestCase("the Doctor", "the doctor")]
            [TestCase("A", "a")]
            [TestCase("1!!", "1!!")]
            [TestCase("Ω", "ω")]
            public void IgnoreCaseTests(string casingOne, string casingStyleTwo)
            {
                Assert.That(_hashAlgorithm.HashStringCaseInsensitive(casingOne), Is.EqualTo(_hashAlgorithm.HashStringCaseInsensitive(casingStyleTwo)));
            }

            [Test]
            public void NullShouldThrowException()
            {
                Assert.That(()=> _hashAlgorithm.HashStringCaseInsensitive(null), Throws.Exception);
            }

            [Test]
            public void EmptyShouldReturnDefaultHashValue()
            {
                var defaultHash = 2166136261;
                Assert.That(_hashAlgorithm.HashStringCaseInsensitive(""), Is.EqualTo((int) defaultHash));
            }
        }
    }
}
