using System;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
    public class PowerOfTwoHelperTests
    {

        [TestCase(2, 2)]
        [TestCase(250, 256)]
        [TestCase(100, 128)]
        [TestCase(1000, 1024)]
        [TestCase(1024, 1024)]
        [TestCase(1500, 2048)]
        [TestCase(60000, 65536)]
        [TestCase(100000, 131072)]
        [TestCase(1048570, 1048576)]
        [TestCase(4194000, 4194304)]
        [TestCase(1073741800, 1073741824)]
        public void FindNearestPowerOfTwoForGivenValue(int value, int expected)
        {
            Assert.That(value.FindNearestPowerOfTwoEqualOrGreaterThan(), Is.EqualTo(expected));
        }

        [TestCase(2, 2)]
        [TestCase(5, 4)]
        [TestCase(300, 256)]
        [TestCase(140, 128)]
        [TestCase(1050, 1024)]
        [TestCase(1024, 1024)]
        [TestCase(3000, 2048)]
        [TestCase(70000, 65536)]
        [TestCase(140000, 131072)]
        [TestCase(1148570, 1048576)]
        [TestCase(4494000, 4194304)]
        [TestCase(1273741800, 1073741824)]
        public void FindNearestPowerOfTwoLessThanForGivenValue(int value, int expected)
        {
            Assert.That(value.FindNearestPowerOfTwoEqualOrLessThan(), Is.EqualTo(expected));
        }

        [Test]
        public void FindNearestPowerOfTwoShouldThrowArumentExceptionWhenTargetValueWouldBegreaterThanMaxInt32()
        {
            Assert.That(() => (int.MaxValue - 1).FindNearestPowerOfTwoEqualOrGreaterThan(), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void FindNearestPowerOfTwoShouldThrowArumentExceptionWhenTargetValueIsLessThan0()
        {
            Assert.That(() => (-100).FindNearestPowerOfTwoEqualOrGreaterThan(), Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void FindNearestPowerOfTwoEqualOrLessThanThrowsArgumentExceptionWhenValueIsLessThanOrEqualZero(int value)
        {
            Assert.That(()=>value.FindNearestPowerOfTwoEqualOrLessThan(),Throws.Exception);
        }

        [TestCase((uint) 2,true)]
        [TestCase((uint) 3, false)]
        [TestCase((uint) 1024, true)]
        [TestCase((uint) 999, false)]
        [TestCase((uint) 1073741824, true)]
        [TestCase((uint) 2073741824, false)]
        public void IsPowerOfTwoTests(uint value, bool expected)
        {
            Assert.That(value.IsPowerOfTwo(), Is.EqualTo(expected));
        }
    }
}
