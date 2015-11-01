using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
#pragma warning disable 420

namespace Ruzzie.Caching
{
    /// <summary>
    /// Cirular buffer that overwrites values when the capacity is reached.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class ConcurrentCircularOverwriteBuffer<T>
    {
        private readonly int _capacity;
        private volatile T[] _buffer;
        private volatile int _writeHeader;
        private volatile int _readHeader;
        private volatile int _count;
        private readonly int _indexMask;        

        /// <summary>
        /// Initializes a new instance of the <see cref="ConcurrentCircularOverwriteBuffer{T}"/> class.
        /// </summary>
        /// <param name="capacity">The desired size. Internally this will always be set to a power of 2 for performance.</param>
        /// <exception cref="ArgumentOutOfRangeException">size;Size has to be greater or equal to 2.</exception>
        public ConcurrentCircularOverwriteBuffer(int capacity = 1024)
        {
            if (capacity < 2)
            {
                throw new ArgumentOutOfRangeException("capacity","Size has to be greater or equal to 2.");
            }
            _capacity = capacity.FindNearestPowerOfTwo();
            _buffer = new T[_capacity];
            _indexMask =  (_capacity - 1);

            _writeHeader = _indexMask;
            _readHeader  = _indexMask;
        }

        public int Count { get { return _count; } }

        public void CopyTo(Array array, int index)
        {
            _buffer.CopyTo(array, index);
        }

        public void WriteNext(T value)
        {
            int writeHeaderLocal = _writeHeader;

            while (Interlocked.CompareExchange(ref _writeHeader, NextWriteIndex(writeHeaderLocal,_indexMask), writeHeaderLocal) != writeHeaderLocal)
            {
                SpinWait();
                writeHeaderLocal = _writeHeader;
            }
          
            _buffer[writeHeaderLocal] = value;

            if (_count < _capacity)
            {               
                Interlocked.Increment(ref _count);
            }                       
        }

        public T ReadNext()
        {
            T value;
            if (ReadNext(out value))
            {
                return value;
            }
            throw new Exception("Error there is no next value.");
        }

        public bool ReadNext(out T value)
        {
            if (_count == 0)
            {
                value = default(T);
                return false;
            }
       
            int readHeaderLocal = _readHeader;
          
            while (Interlocked.CompareExchange(ref _readHeader, NextReadIndex(readHeaderLocal, _indexMask), readHeaderLocal) != readHeaderLocal)
            {
                SpinWait();
                readHeaderLocal = _readHeader;
            }
            value = _buffer[readHeaderLocal];

            Interlocked.Decrement(ref _count);
           
            return true;
        }

        private static int NextReadIndex(int currentHeader, int indexMask)
        {
            return (currentHeader + 1) & (indexMask);
        }

        private static int NextWriteIndex(int currentHeader, int indexMask)
        {
            return (currentHeader + 1) & (indexMask);
        }

#if PORTABLE
        [SuppressMessage("ReSharper", "RedundantAssignment")]
        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        private static void SpinWait()
        {
            int op = 4;
            op = op << 1;
        }
#else
        [SuppressMessage("ReSharper", "StaticMemberInGenericType")]
        private static readonly int SpinWaitIterations = 1;
        private static void SpinWait()
        {
            Thread.SpinWait(SpinWaitIterations);
        }
#endif
    }
}