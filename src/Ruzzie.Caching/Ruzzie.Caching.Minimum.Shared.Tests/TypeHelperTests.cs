using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;

namespace Ruzzie.Caching.Tests
{
    [TestFixture]
    public class TypeHelperTests
    {
        private class SizeOfComplexType
        {
            // ReSharper disable UnusedMember.Local
            public int Count { get; set; }
            public byte Flag { get; set; }
            // ReSharper restore UnusedMember.Local
        }

        private enum SizeOfEnum
        {
            // ReSharper disable UnusedMember.Local
            None,
            A,
            B
            // ReSharper restore UnusedMember.Local
        }

        [TestCase((sbyte) 1, 1)]
        [TestCase((byte) 1, 1)]
        [TestCase((short) 1, 2)]
        [TestCase((ushort) 1, 2)]
        [TestCase(1, 4)]
        [TestCase((uint) 1, 4)]
        [TestCase((long) 1, 8)]
        [TestCase((ulong) 1, 8)]
        [TestCase((float) 1, 4)]
        [TestCase((double) 1, 8)]
        [TestCase(true, 1)]
        public void ValueTypeSizeTests(ValueType value, int expectedSize)
        {
            Assert.That(TypeHelper.SizeOf(value), Is.EqualTo(expectedSize));
        }

        [TestCase(new sbyte[] {1, 2}, 30)]
        [TestCase(new byte[] {1, 2}, 30)]
        [TestCase(new short[] {1, 2}, 32)] //32 || 24
        [TestCase(new ushort[] {1, 2}, 32)]
        [TestCase(new[] {1, 2}, 36)] //32
        [TestCase(new uint[] {1, 2}, 36)] //32
        [TestCase(new long[] {1, 2}, 44)]
        [TestCase(new ulong[] {1, 2}, 44)] //40
        [TestCase(new float[] {1, 2}, 36)]
        [TestCase(new double[] {1, 2}, 44)]
        [TestCase(new[] {true, false}, 30)]
        [TestCase(new[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16}, 92)] //88
        [TestCase(new short[] {1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16}, 60)] //56
        public void ArraySizeTests(object value, int expectedSize)
        {
            Assert.That(TypeHelper.SizeOf(value), Is.EqualTo(expectedSize));
        }

        [TestCase(0, 8)]
        [TestCase(1, 8)]
        [TestCase(7, 8)]
        [TestCase(15, 16)]
        [TestCase(20, 24)]
        [TestCase(30, 32)]
        [TestCase(32, 32)]
        public void RoundUpToNearestMultipleOfEightTests(int number, int expected)
        {
            Assert.That(number.RoundUpToNearest(8), Is.EqualTo(expected));
        }

        [TestCase(0, 4)]
        [TestCase(1, 4)]
        [TestCase(7, 8)]
        [TestCase(10, 12)]
        [TestCase(15, 16)]
        [TestCase(20, 20)]
        [TestCase(30, 32)]
        public void RoundUpToNearestMultipleOfFourTests(int number, int expected)
        {
            Assert.That(number.RoundUpToNearest(4), Is.EqualTo(expected));
        }

        [TestCase(0, false, 12)]
        [TestCase(0, true, 24)]
        [TestCase(4, false, 12)] //One Int 32bit
        [TestCase(4, true, 24)] //One Int 64bit
        [TestCase(8, true, 24)] //Two Ints 64bit
        [TestCase(8, false, 16)] //Two Ints 32bit
        [TestCase(12, true, 32)] //Three Ints 64bit
        [TestCase(12, false, 20)] //Three Ints 32bit
        [TestCase(16, true, 32)] //Mixed 16b 64bit
        [TestCase(16, false, 24)] //Mixed 16b 32bit
        public void TestPacking(int allTypeSize, bool is64Bit, int expectedObjectSize)
        {
            int actualSize = SizeHelper.CalculateActualSizeInBytesForType(allTypeSize, is64Bit);

            Assert.That(actualSize, Is.EqualTo(expectedObjectSize));
        }

        [Test]
        public void ArrayOfObjectSizeTests()
        {
            object[] value =
            {
                new object(), new object(), new object(), new object(), new object(), new object(), new object(), new object(), new object(),
                new object(), new object(), new object(), new object(), new object(), new object(), new object()
            };
            Assert.That(TypeHelper.SizeOf(value), Is.EqualTo(544));
        }

        [Test]
        public void ArraySizeDecimalTest()
        {
            Assert.That(TypeHelper.SizeOf(new decimal[] {1, 2}), Is.EqualTo(60));
        }

        [Test]
        public void ArraySizeDefaultSizeWhenEmptyObjectIsPassed()
        {
            int[] myArray = default(int[]);
            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.That(TypeHelper.SizeOf(typeof (int[]), myArray), Is.EqualTo(384));
        }

        [Test]
        public void ComplexTypeSize()
        {
            Assert.That(TypeHelper.SizeOf(new SizeOfComplexType()), Is.EqualTo(24));
        }

        [Test]
        public void DecimalSizeTest()
        {
            Assert.That(TypeHelper.SizeOf(decimal.One), Is.EqualTo(16));
        }

        [Test]
        public void DictionaryDefaultSizeTests()
        {
            IDictionary<int, float> testDictionary = null;

            // ReSharper disable once ExpressionIsAlwaysNull
            Assert.That(TypeHelper.SizeOf(typeof (IDictionary<int, float>), testDictionary), Is.EqualTo(24));
        }

        [Test]
        public void FlashEntrySizeTests_IntGuid()
        {
            FlashCache<int, Guid>.FlashEntry entry = new FlashCache<int, Guid>.FlashEntry(1, 10, Guid.NewGuid());

            Assert.That(TypeHelper.SizeOf(entry), Is.EqualTo(40));
        }

        [Test]
        public void FlashEntrySizeTests_IntInt()
        {
            FlashCache<int, int>.FlashEntry entry = new FlashCache<int, int>.FlashEntry(1, 10, 1234);

            Assert.That(TypeHelper.SizeOf(entry), Is.EqualTo(32));
        }

        [Test]
        public void FlashEntrySizeTests_ObjectObject()
        {
            FlashCache<object, object>.FlashEntry entry = new FlashCache<object, object>.FlashEntry(1, new object(), new object());

            Assert.That(TypeHelper.SizeOf(entry), Is.EqualTo(88)); //int 4, object 32, object 32, type 24
        }

        [Test]
        public void FlashEntrySizeTests_StringGuid_StringSize_10()
        {
            FlashCache<string, Guid>.FlashEntry entry = new FlashCache<string, Guid>.FlashEntry(1, "1234567890", default(Guid));

            Assert.That(TypeHelper.SizeOf(entry), Is.EqualTo(96));
        }

        [Test]
        public void FlashEntrySizeTests_StringGuid_StringSize_20()
        {
            FlashCache<string, Guid>.FlashEntry entry = new FlashCache<string, Guid>.FlashEntry(1, default(string), default(Guid));

            Assert.That(TypeHelper.SizeOf(entry), Is.EqualTo(120));
        }

        [Test]
        public void FlashEntrySizeTests_StringGuid_StringSize_30()
        {
            FlashCache<string, Guid>.FlashEntry entry = new FlashCache<string, Guid>.FlashEntry(1, "_ ".PadLeft(30, 'I'), default(Guid));

            Assert.That(TypeHelper.SizeOf(entry), Is.EqualTo(136));
        }

        [Test]
        public void GenericDictionarySizeTests()
        {
            Dictionary<int, float> testDictionary = new Dictionary<int, float>();
            testDictionary[1] = float.MinValue;
            testDictionary[2] = float.MaxValue;

            Assert.That(TypeHelper.SizeOf(testDictionary), Is.EqualTo(200));
        }

        [Test]
        public void GenericDictionarySizeTestsDeclaredAsInterface()
        {
            IDictionary<int, float> testDictionary = new Dictionary<int, float>();
            testDictionary[1] = float.MinValue;
            testDictionary[2] = float.MaxValue;

            Assert.That(TypeHelper.SizeOf(testDictionary), Is.EqualTo(200));
        }

        [Test]
        public void GuidTypeSize()
        {
            Assert.That(TypeHelper.SizeOf(new Guid()), Is.EqualTo(16));
        }

        [Test]
        public void NonGenericDictionarySizeTests()
        {
            Hashtable testDictionary = new Hashtable();
            testDictionary[1] = float.MinValue;
            testDictionary[2] = float.MaxValue;

            Assert.That(TypeHelper.SizeOf(testDictionary), Is.EqualTo(296));
        }

        [Test]
        public void NonGenericDictionarySizeWithObjectsTests()
        {
            Hashtable testDictionary = new Hashtable();
            testDictionary[new object()] = new object();
            testDictionary[new object()] = new object();

            Assert.That(TypeHelper.SizeOf(testDictionary), Is.EqualTo(296));
        }

        [Test]
        public void SizeOfEnumTest()
        {
            Assert.That(TypeHelper.SizeOf(SizeOfEnum.B), Is.EqualTo(4));
        }

        [Test]
        public void SizeOfStringArray()
        {
            int sizeInBytes = TypeHelper.SizeOf(typeof (string[]), default(string[]));

            Assert.That(sizeInBytes, Is.EqualTo(7152));
        }

        [Test]
        public void SizeOfStringTest()
        {
            Assert.That(TypeHelper.SizeOf("MyTestString"), Is.EqualTo(64));
        }

        [Test]
        public void SizeOfStringTest_WithSize_10()
        {
            Assert.That(TypeHelper.SizeOf("1234567890"), Is.EqualTo(56));
        }

        [Test]
        public void Smokey()
        {
            int sizeInBytes = TypeHelper.SizeOf(typeof (KeyValuePair<string, float>), default(KeyValuePair<string, float>));

            Assert.That(sizeInBytes, Is.EqualTo(80));
        }
    }
}