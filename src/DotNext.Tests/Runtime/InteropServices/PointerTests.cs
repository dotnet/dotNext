﻿using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DotNext.Runtime.InteropServices;

public sealed class PointerTests : Test
{
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
        buffer.AsSpan().ForEach(static (ref byte value, int _) =>
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
        buffer.AsSpan().ForEach(static (ref byte value, int _) =>
        {
            if (value == 1)
                value = 20;
        });
        ms.Position = 0;
        Equal(6, await ptr.ReadFromAsync(ms, array.Length));
        Equal(20, ptr[0]);
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

    [Fact]
    public static unsafe void ReadWrite()
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
    public static unsafe void ReadWriteUnaligned()
    {
        var array = new ushort[] { 1, 2, 3 };
        fixed (ushort* p = array)
        {
            var ptr = new Pointer<ushort>(p);
            Equal(new IntPtr(p), ptr.Address);
            ptr.SetUnaligned(20);
            Equal(20, array[0]);
            Equal(20, ptr.GetUnaligned());
            ptr.SetUnaligned(30, 1);
            Equal(30, array[1]);
            Equal(30, ptr.GetUnaligned(1));
            ptr.SetUnaligned(42, 0);
            Equal(42, array[0]);
            Equal(42, ptr.GetUnaligned(0));
        }
    }

    [Fact]
    public static unsafe void Fill()
    {
        Pointer<int> ptr = stackalloc int[10];
        Equal(0, ptr[0]);
        Equal(0, ptr[8]);
        ptr.Fill(42, 10);
        Equal(42, ptr[0]);
        Equal(42, ptr[9]);
        Pointer<int> ptr2 = stackalloc int[10];
        ptr.CopyTo(ptr2, 10);
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
        Throws<NullReferenceException>(() => ptr.Value = 10);
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
    public static unsafe void Operators()
    {
        var ptr1 = new Pointer<int>(new nint(42));
        var ptr2 = new Pointer<int>(new nint(46));
        True(ptr1 != ptr2);
        False(ptr1 == ptr2);
        
        ptr2 -= new nint(1);
        Equal(new nint(42), ptr2);
        False(ptr1 != ptr2);
        Equal(new nint(42), ptr1);
        True(new nint(42).ToPointer() == ptr1);
        if (ptr1) { }
        else Fail("Unexpected zero pointer");
        
        ptr2 = default;
        if (ptr2) Fail("Unexpected non-zero pointer");
        True(!ptr2);

        ptr1 += 2U;
        Equal(new nint(50), ptr1);
        ptr1 += 1L;
        Equal(new nint(54), ptr1);
        ptr1 += new nint(2);
        Equal(new nint(62), ptr1);

        ptr1 = new Pointer<int>(new nuint(56U));
        Equal(new nuint(56U), ptr1);

        ptr1 += (nint)1;
        Equal(new nint(60), ptr1);
        ptr1 -= (nint)1;
        Equal(new nint(56), ptr1);

        True(ptr1 > ptr2);
        True(ptr1 >= ptr2);

        False(ptr1 < ptr2);
        False(ptr1 <= ptr2);
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
        True(default(Pointer<int>).ToMemoryOwner(2).Memory.IsEmpty);
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
        Equal(ptr.Address, new IntPtr(Pointer.Unbox(obj)));
    }

    [Fact]
    public static void CompareToMethod()
    {
        IComparable<Pointer<int>> x = new Pointer<int>(9);
        Equal(1, x.CompareTo(Pointer<int>.Null));
        Equal(-1, x.CompareTo(new(10)));
        Equal(0, x.CompareTo(new(9)));
    }

    [Fact]
    public static unsafe void AlignmentCheck()
    {
        var i = 0;
        True(new Pointer<int>(&i).IsAligned);
    }

    [Fact]
    public static void BitwiseComparison()
    {
        using var mem1 = new UnmanagedMemory<UInt128>(UInt128.MaxValue);
        using var mem2 = new UnmanagedMemory<UInt128>(UInt128.MaxValue);
        True(mem1.Pointer.BitwiseCompare(mem2.Pointer, 1U) is 0);

        mem2.Pointer.Value--;

        True(mem1.Pointer.BitwiseCompare(mem2.Pointer, 1U) > 0);
    }

    [Fact]
    public static unsafe void ClearValue()
    {
        var i = 42;

        Pointer<int> ptr = &i;
        ptr.Clear();

        Equal(0, i);
    }

    [Fact]
    public static unsafe void SpanOverElements()
    {
        const int count = 2;
        Pointer<int> ptr = stackalloc int[count];
        var elements = ptr.AsSpan(count);
        elements[0] = 42;
        elements[1] = 43;

        Equal(42, ptr[0]);
        Equal(43, ptr[1]);
    }

    [Fact]
    public static unsafe void PointerMarshalling()
    {
        Pointer<int> ptr = stackalloc int[1];
        nint address = ptr;
        Equal(address, PointerMarshaller<int>.ConvertToUnmanaged(ptr));
        Equal(ptr, PointerMarshaller<int>.ConvertToManaged(address));
    }
}