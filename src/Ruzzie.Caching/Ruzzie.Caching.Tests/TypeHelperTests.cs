using System.Collections.Generic;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
    public class TypeHelperTests
    {
        [Test]
        public void Smokey()
        {
            int sizeInBytes = TypeHelper.SizeOf(typeof (KeyValuePair<string, float>));
            
            Assert.That(sizeInBytes,Is.EqualTo(8));
        }

        [Test]
        public void SizeOfArray()
        {
            int sizeInBytes = TypeHelper.SizeOf(typeof(string[]));

            Assert.That(sizeInBytes, Is.EqualTo(8));
        }


    }
}
