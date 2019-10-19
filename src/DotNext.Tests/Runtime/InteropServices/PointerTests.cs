namespace DotNext.Runtime.InteropServices
{
    using Threading;

    public sealed class PointerTests : Assert
    {
        [Fact]
        public static unsafe void BitwiseOperationsTest()
        {
            var array1 = new ushort[] { 1, 2, 3 };
            var array2 = new ushort[] { 1, 2, 3 };
            fixed (ushort* p1 = array1, p2 = array2)
            {
                var ptr1 = new Pointer<ushort>(p1);
                var ptr2 = new Pointer<ushort>(p2);
                True(ptr1.BitwiseEquals(ptr2, array1.Length));
                Equal(0, ptr1.BitwiseCompare(ptr2, array1.Length));
                array2[1] = 55;
                False(ptr1.BitwiseEquals(ptr2, array1.Length));
                NotEqual(0, ptr1.BitwiseCompare(ptr2, array1.Length));
            }
        }

        [Fact]
        public static unsafe void StreamInteropTest()
        {
            var array = new ushort[] { 1, 2, 3 };
            fixed (ushort* p = array)
            {
                var ptr = new Pointer<ushort>(p);
                var ms = new MemoryStream();
                ptr.WriteTo(ms, array.LongLength);
                Equal(6L, ms.Length);
                ms.Dispose();
            }
        }

        [Fact]
        public static unsafe void ArrayInteropTest()
        {
            var array = new ushort[] { 1, 2, 3 };
            fixed (ushort* p = array)
            {
                var ptr = new Pointer<ushort>(p);
                var dest = new ushort[array.LongLength];
                Equal(3L, ptr.WriteTo(dest, 0, array.LongLength));
                Equal(array, dest);
                dest[0] = 50;
                Equal(3L, ptr.ReadFrom(dest, 0, dest.LongLength));
                Equal(50, ptr.Value);
                Equal(50, array[0]);
            }
        }

        [Fact]
        public static unsafe void SwapTest()
        {
            var array = new ushort[] { 1, 2 };
            fixed (ushort* p = array)
            {
                var ptr1 = new Pointer<ushort>(p);
                var ptr2 = ptr1 + 1;
                Equal(1, ptr1.Value);
                Equal(2, ptr2.Value);
                ptr1.Swap(ptr2);
            }
            Equal(2, array[0]);
            Equal(1, array[1]);
        }

        [Fact]
        public static unsafe void VolatileReadWriteTest()
        {
            Pointer<long> ptr = stackalloc long[3];
            ptr.VolatileWrite(1);
            Equal(1, ptr.Value);
            ptr.AddValue(10);
            Equal(11, ptr.Value);
            Equal(11, ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(10, ptr.Value);
            Equal(10, ptr.VolatileRead());
            True(ptr.CompareAndSetValue(10, 12));
            Equal(12, ptr.Value);
            False(ptr.CompareAndSetValue(10, 20));
            Equal(12, ptr.Value);
        }

        [Fact]
        public static unsafe void ReadWriteTest()
        {
            var array = new ushort[] { 1, 2, 3 };
            fixed (ushort* p = array)
            {
                var ptr = new Pointer<ushort>(p);
                Equal(new IntPtr(p), ptr.Address);
                ptr.Value = 20;
                Equal(20, array[0]);
                Equal(20, ptr.Value);
                ++ptr;
                ptr.Value = 30;
                Equal(30, array[1]);
                Equal(30, ptr.Value);
                --ptr;
                ptr.Value = 42;
                Equal(42, array[0]);
                Equal(42, ptr.Value);
            }
        }

        [Fact]
        public static unsafe void Fill()
        {
            Pointer<int> ptr = stackalloc int[10];
            Equal(0, ptr[0]);
            Equal(0, ptr[8]);
            ptr.Fill(42, 10L);
            Equal(42, ptr[0]);
            Equal(42, ptr[9]);
        }
    }
}
