using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    using Threading;

    [ExcludeFromCodeCoverage]
    public sealed class PointerTests : Test
    {
        [Fact]
        public static unsafe void BitwiseOperations()
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
        public static void StreamInterop()
        {
            var array = new ushort[] { 1, 2, 3 }.AsMemory();
            using var pinned = array.Pin();
            using var ms = new MemoryStream();
            var ptr = (Pointer<ushort>)pinned;
            ptr.WriteTo(ms, array.Length);
            Equal(6L, ms.Length);
            True(ms.TryGetBuffer(out var buffer));
            buffer.Array.ForEach(static (ref byte value, long index) =>
            {
                if (value == 1)
                    value = 20;
            });
            ms.Position = 0;
            Equal(6, ptr.ReadFrom(ms, array.Length));
            Equal(20, ptr[0]);
        }

        [Fact]
        public static async Task StreamInteropAsync()
        {
            var array = new ushort[] { 1, 2, 3 }.AsMemory();
            using var pinned = array.Pin();
            using var ms = new MemoryStream();
            var ptr = (Pointer<ushort>)pinned;
            await ptr.WriteToAsync(ms, array.Length);
            Equal(6L, ms.Length);
            True(ms.TryGetBuffer(out var buffer));
            buffer.Array.ForEach(static (ref byte value, long index) =>
            {
                if (value == 1)
                    value = 20;
            });
            ms.Position = 0;
            Equal(6, await ptr.ReadFromAsync(ms, array.Length));
            Equal(20, ptr[0]);
        }

        [Fact]
        public static unsafe void ArrayInterop()
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
        public static unsafe void ArrayInteropWithOffset()
        {
            var array = new ushort[] { 1, 2, 3 };
            fixed (ushort* p = array)
            {
                var ptr = new Pointer<ushort>(p);
                var dest = new ushort[array.LongLength];
                Equal(1L, ptr.WriteTo(dest, 2L, 1L));
                NotEqual(array, dest);
                Equal(new ushort[] { 0, 0, 1 }, dest);
                dest[2] = 50;
                Equal(1L, ptr.ReadFrom(dest, 2L, 1L));
                Equal(50, ptr.Value);
                Equal(50, array[0]);
            }
        }

        [Fact]
        public static unsafe void Swap()
        {
            var array = stackalloc ushort[] { 1, 2 };
            var ptr1 = new Pointer<ushort>(array);
            var ptr2 = ptr1 + 1;
            Equal(1, ptr1.Value);
            Equal(2, ptr2.Value);
            ptr1.Swap(ptr2);
            Equal(2, array[0]);
            Equal(1, array[1]);
        }

#if !NETCOREAPP3_1
        [Fact]
        public static unsafe void VolatileReadWriteUInt64()
        {
            Pointer<ulong> ptr = stackalloc ulong[3];
            ptr.VolatileWrite(1);
            Equal(1UL, ptr.Value);
            Equal(1UL, ptr.Get());
            ptr.AddValue(10);
            Equal(11UL, ptr.Value);
            Equal(11UL, ptr.Get());
            Equal(11UL, ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(10UL, ptr.Value);
            Equal(10UL, ptr.VolatileRead());
            True(ptr.CompareAndSetValue(10, 12));
            Equal(12UL, ptr.Value);
            False(ptr.CompareAndSetValue(10, 20));
            Equal(12UL, ptr.Value);
            static ulong Sum(ulong x, ulong y) => x + y;
            Equal(32UL, ptr.AccumulateAndGetValue(20L, Sum));
            Equal(32UL, ptr.Value);
            Equal(32UL, ptr.GetAndAccumulateValue(8L, &Sum));
            Equal(40UL, ptr.Value);
        }

        [Fact]
        public static unsafe void VolatileReadWriteUInt32()
        {
            Pointer<uint> ptr = stackalloc uint[3];
            ptr.VolatileWrite(1);
            Equal(1U, ptr.Value);
            ptr.AddValue(10);
            Equal(11U, ptr.Value);
            Equal(11U, ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(10U, ptr.Value);
            Equal(10U, ptr.VolatileRead());
            True(ptr.CompareAndSetValue(10, 12));
            Equal(12U, ptr.Value);
            False(ptr.CompareAndSetValue(10, 20));
            Equal(12U, ptr.Value);
            static uint Sum(uint x, uint y) => x + y;
            Equal(32U, ptr.AccumulateAndGetValue(20, Sum));
            Equal(32U, ptr.Value);
            Equal(32U, ptr.GetAndAccumulateValue(8, &Sum));
            Equal(40U, ptr.Value);
        }
#endif

        [Fact]
        public static unsafe void VolatileReadWriteInt64()
        {
            Pointer<long> ptr = stackalloc long[3];
            ptr.VolatileWrite(1);
            Equal(1, ptr.Value);
            Equal(1, ptr.Get());
            ptr.AddValue(10);
            Equal(11, ptr.Value);
            Equal(11, ptr.Get());
            Equal(11, ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(10, ptr.Value);
            Equal(10, ptr.VolatileRead());
            True(ptr.CompareAndSetValue(10, 12));
            Equal(12, ptr.Value);
            False(ptr.CompareAndSetValue(10, 20));
            Equal(12, ptr.Value);
            static long Sum(long x, long y) => x + y;
            Equal(32L, ptr.AccumulateAndGetValue(20L, Sum));
            Equal(32L, ptr.Value);
            Equal(32L, ptr.GetAndAccumulateValue(8L, &Sum));
            Equal(40L, ptr.Value);
        }

        [Fact]
        public static unsafe void VolatileReadWriteInt32()
        {
            Pointer<int> ptr = stackalloc int[3];
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
            static int Sum(int x, int y) => x + y;
            Equal(32, ptr.AccumulateAndGetValue(20, Sum));
            Equal(32, ptr.Value);
            Equal(32, ptr.GetAndAccumulateValue(8, &Sum));
            Equal(40, ptr.Value);
        }

        [Fact]
        public static unsafe void VolatileReadWriteIntPtr()
        {
            Pointer<IntPtr> ptr = stackalloc IntPtr[3];
            ptr.VolatileWrite(new IntPtr(1));
            Equal(new IntPtr(1), ptr.Value);
            ptr.AddValue(new IntPtr(10));
            Equal(new IntPtr(11), ptr.Value);
            Equal(new IntPtr(11), ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(new IntPtr(10), ptr.Value);
            Equal(new IntPtr(10), ptr.VolatileRead());
            True(ptr.CompareAndSetValue(new IntPtr(10), new IntPtr(12)));
            Equal(new IntPtr(12), ptr.Value);
            False(ptr.CompareAndSetValue(new IntPtr(10), new IntPtr(20)));
            Equal(new IntPtr(12), ptr.Value);
            static nint Sum(nint x, nint y) => x + y;
            Equal(new IntPtr(32), ptr.AccumulateAndGetValue(new IntPtr(20), Sum));
            Equal(new IntPtr(32), ptr.Value);
            Equal(new IntPtr(32), ptr.GetAndAccumulateValue(new IntPtr(8), &Sum));
            Equal(new IntPtr(40), ptr.Value);
        }

        [Fact]
        public static unsafe void VolatileReadWriteFloat32()
        {
            Pointer<float> ptr = stackalloc float[3];
            ptr.VolatileWrite(1F);
            Equal(1F, ptr.Value);
            ptr.AddValue(10F);
            Equal(11F, ptr.Value);
            Equal(11F, ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(10F, ptr.Value);
            Equal(10F, ptr.VolatileRead());
            True(ptr.CompareAndSetValue(10F, 12F));
            Equal(12F, ptr.Value);
            False(ptr.CompareAndSetValue(10F, 20F));
            Equal(12F, ptr.Value);
        }

        [Fact]
        public static unsafe void VolatileReadWriteFloat64()
        {
            Pointer<double> ptr = stackalloc double[3];
            ptr.VolatileWrite(1D);
            Equal(1D, ptr.Value);
            ptr.AddValue(10F);
            Equal(11D, ptr.Value);
            Equal(11D, ptr.VolatileRead());
            ptr.DecrementValue();
            Equal(10D, ptr.Value);
            Equal(10D, ptr.VolatileRead());
            True(ptr.CompareAndSetValue(10D, 12D));
            Equal(12D, ptr.Value);
            False(ptr.CompareAndSetValue(10D, 20D));
            Equal(12D, ptr.Value);
        }

        [Fact]
        public static unsafe void VolatileReadWriteUIntPtr()
        {
            Pointer<nuint> ptr = stackalloc UIntPtr[3];
            ptr.VolatileWrite(new UIntPtr(1));
            Equal(new UIntPtr(1), (UIntPtr)ptr.Value);
            ptr.Value = ptr.Value + 10;
            Equal(new UIntPtr(11), (UIntPtr)ptr.Value);
            Equal(new UIntPtr(11), ptr.VolatileRead());
        }

        [Fact]
        public static unsafe void VolatileReadWriteInt16()
        {
            Pointer<short> ptr = stackalloc short[3];
            ptr.VolatileWrite(1);
            Equal(1, ptr.Value);
            Equal(1, ptr.Get());
            ptr.Value += 10;
            Equal(11, ptr.Value);
            Equal(11, ptr.Get());
            Equal(11, ptr.VolatileRead());
        }

        [Fact]
        public static unsafe void VolatileReadWriteUInt16()
        {
            Pointer<ushort> ptr = stackalloc ushort[3];
            ptr.VolatileWrite(1);
            Equal(1, ptr.Value);
            Equal(1, ptr.Get());
            ptr.Value += 10;
            Equal(11, ptr.Value);
            Equal(11, ptr.Get());
            Equal(11, ptr.VolatileRead());
        }

        [Fact]
        public static unsafe void VolatileReadWriteUInt8()
        {
            Pointer<byte> ptr = stackalloc byte[3];
            ptr.VolatileWrite(1);
            Equal(1, ptr.Value);
            Equal(1, ptr.Get());
            ptr.Value += 10;
            Equal(11, ptr.Value);
            Equal(11, ptr.Get());
            Equal(11, ptr.VolatileRead());
        }

        [Fact]
        public static unsafe void VolatileReadWriteInt8()
        {
            Pointer<sbyte> ptr = stackalloc sbyte[3];
            ptr.VolatileWrite(1);
            Equal(1, ptr.Value);
            Equal(1, ptr.Get());
            ptr.Value += 10;
            Equal(11, ptr.Value);
            Equal(11, ptr.Get());
            Equal(11, ptr.VolatileRead());
        }

        [Fact]
        public static unsafe void ReadWrite()
        {
            var array = new ushort[] { 1, 2, 3 };
            fixed (ushort* p = array)
            {
                var ptr = new Pointer<ushort>(p);
                Equal(new IntPtr(p), (IntPtr)ptr.Address);
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
        public static unsafe void ReadWrite2()
        {
            var array = new ushort[] { 1, 2, 3 };
            fixed (ushort* p = array)
            {
                var ptr = new Pointer<ushort>(p);
                Equal(new IntPtr(p), (IntPtr)ptr.Address);
                ptr.Set(20);
                Equal(20, array[0]);
                Equal(20, ptr.Get(0));
                ptr.Set(30, 1L);
                Equal(30, array[1]);
                Equal(30, ptr.Get(1));
                ptr.Set(42, 0);
                Equal(42, array[0]);
                Equal(42, ptr.Get(0));
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
            Pointer<int> ptr2 = stackalloc int[10];
            ptr.WriteTo(ptr2, 10);
            Equal(42, ptr2[0]);
            Equal(42, ptr2[9]);
            ptr.Clear(10);
            Equal(0, ptr[0]);
            Equal(0, ptr[8]);
        }

        [Fact]
        public static unsafe void ToStreamConversion()
        {
            Pointer<byte> ptr = stackalloc byte[] { 10, 20, 30 };
            using var stream = ptr.AsStream(3);
            var bytes = new byte[3];
            Equal(3, stream.Read(bytes, 0, 3));
            Equal(10, bytes[0]);
            Equal(20, bytes[1]);
            Equal(30, bytes[2]);
        }

        [Fact]
        public static void NullPointer()
        {
            var ptr = default(Pointer<int>);
            Throws<NullPointerException>(() => ptr[0] = 10);
            Throws<NullPointerException>(() => ptr.Value = 10);
            Throws<NullPointerException>(() => ptr.Set(10));
            Throws<NullPointerException>(() => ptr.Set(10, 0));
            Empty(ptr.ToByteArray(10));
            True(ptr.Bytes.IsEmpty);
            Equal(Pointer<int>.Null, ptr);
            True(Pointer<int>.Null.IsNull);
        }

        [Fact]
        public static unsafe void ToArray()
        {
            Pointer<byte> ptr = stackalloc byte[] { 1, 2, 3 };
            var array = ptr.ToByteArray(3);
            Equal(3, array.Length);
            Equal(1, array[0]);
            Equal(2, array[1]);
            Equal(3, array[2]);
            NotEqual(Pointer<byte>.Null, ptr);
            array = ptr.ToArray(3);
            Equal(3, array.Length);
            Equal(1, array[0]);
            Equal(2, array[1]);
            Equal(3, array[2]);
        }

        [Fact]
        public static unsafe void Alignment()
        {
            Pointer<int> ptr = default;
            True(ptr.IsAligned);
            var a = 20;
            ptr = &a;
            True(ptr.IsAligned);
            decimal d = 20;
            ptr = (int*)(((byte*)&d) + 1);
            False(ptr.IsAligned);
        }

        [Fact]
        public static unsafe void Operators()
        {
            var ptr1 = new Pointer<int>(new IntPtr(42));
            var ptr2 = new Pointer<int>(new IntPtr(46));
            True(ptr1 != ptr2);
            False(ptr1 == ptr2);
            ptr2 -= new IntPtr(1);
            Equal(new IntPtr(42), ptr2);
            False(ptr1 != ptr2);
            Equal(new IntPtr(42), ptr1);
            True(new IntPtr(42).ToPointer() == ptr1);
            if (ptr1) { }
            else throw new Xunit.Sdk.XunitException();
            ptr2 = default;
            if (ptr2) throw new Xunit.Sdk.XunitException();

            ptr1 += 2U;
            Equal(new IntPtr(50), ptr1);
            ptr1 += 1L;
            Equal(new IntPtr(54), ptr1);
            ptr1 += new IntPtr(2);
            Equal(new IntPtr(62), ptr1);

            ptr1 = new Pointer<int>(new UIntPtr(56U));
            Equal(new UIntPtr(56U), ptr1);

            ptr1 += (nint)1;
            Equal(new IntPtr(60), ptr1);
            ptr1 -= (nint)1;
            Equal(new IntPtr(56), ptr1);
        }

        [Fact]
        public static unsafe void Boxing()
        {
            var value = 10;
            IStrongBox box = new Pointer<int>(&value);
            Equal(10, box.Value);
            box.Value = 42;
            Equal(42, box.Value);
        }

        [Fact]
        public static unsafe void Conversion()
        {
            var value = 10;
            Pointer<uint> ptr = new Pointer<int>(&value).As<uint>();
            ptr.Value = 42U;
            Equal(42, value);
        }

        [Fact]
        public static unsafe void ConversionToMemoryOwner()
        {
            Throws<ObjectDisposedException>(() => default(Pointer<int>).ToMemoryOwner(2).Memory);
            Pointer<int> ptr = stackalloc int[10];
            ptr.Clear(10);
            ptr[0] = 12;
            ptr[1] = 24;
            var memory = ptr.ToMemoryOwner(2).Memory.Span;
            Equal(2, memory.Length);
            Equal(12, memory[0]);
            Equal(24, memory[1]);
        }

        [Fact]
        public static unsafe void PinnablePointer()
        {
            Pointer<int> ptr = stackalloc int[1];
            ptr.Value = 42;
            IPinnable pinnable = ptr;
            using var handle = pinnable.Pin(0);
            Equal(42, ((int*)handle.Pointer)[0]);
            pinnable.Unpin();
        }

        [Fact]
        public static unsafe void PointerReflection()
        {
            Pointer<int> ptr = stackalloc int[1];
            ptr.Value = 42;
            var obj = ptr.GetBoxedPointer();
            IsType<Pointer>(obj);
            Equal((IntPtr)ptr.Address, new IntPtr(Pointer.Unbox(obj)));
        }
    }
}
