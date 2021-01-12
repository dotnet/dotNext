using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    [ExcludeFromCodeCoverage]
    public sealed class MemoryTests : Assert
    {
        private Guid field;
        private string str;

        [Fact]
        public void FieldTypedReferenceValueType()
        {
            TypedReference reference = __makeref(field);
            ref Guid g = ref reference.AsRef<Guid>();
            Equal(Guid.Empty, g);
            g = Guid.NewGuid();
            Equal(field, g);
            True(Memory.AreSame(in field, in g));
        }

        [Fact]
        public void FieldTypedReferenceClass()
        {
            TypedReference reference = __makeref(str);
            ref string f = ref reference.AsRef<string>();
            Null(f);
            f = "Hello, world!";
            Equal(str, f);
            True(Memory.AreSame(in str, in f));
        }

        [Fact]
        public static void SwapValues()
        {
            var x = 10;
            var y = 20;
            Memory.Swap(ref x, ref y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public static unsafe void SwapValuesByPointer()
        {
            var x = 10;
            var y = 20;
            Memory.Swap(&x, &y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public static void AddressOfLocal()
        {
            var i = 20;
            True(Memory.AddressOf(i) != IntPtr.Zero);
        }

        [Fact]
        public static unsafe void BitwiseEqualityForByte()
        {
            byte value1 = 10;
            byte value2 = 20;
            False(Memory.Equals(&value1, &value2, sizeof(byte)));
            value2 = 10;
            True(Memory.Equals(&value1, &value2, sizeof(byte)));
        }

        [Fact]
        public static unsafe void BitwiseEqualityForLong()
        {
            var value1 = 10L;
            var value2 = 20L;
            False(Memory.Equals(&value1, &value2, sizeof(long)));
            value2 = 10;
            True(Memory.Equals(&value1, &value2, sizeof(long)));
        }

        [Fact]
        public static unsafe void BitwiseHashCode()
        {
            var i = 42L;
            NotEqual(0, Memory.GetHashCode32(&i, sizeof(long)));
            NotEqual(0L, Memory.GetHashCode64(&i, sizeof(long)));
        }

        private static void NullRefCheck()
        {
            ref readonly var ch = ref default(string).GetRawData();
            Memory.ThrowIfNull(in ch);
        }

        [Fact]
        public static void NullCheck()
        {
            var i = 0L;
            False(Memory.IsNull(in i));
            ref readonly var ch = ref default(string).GetRawData();
            True(Memory.IsNull(in ch));
            Throws<NullReferenceException>(NullRefCheck);
        }

        [Fact]
        public static void ReadWriteString()
        {
            using (var memory = new UnmanagedMemory(1024))
            {
                IntPtr ptr = memory.Pointer;
                Memory.WriteString(ref ptr, "string1");
                Memory.WriteString(ref ptr, "string2");
                Memory.WriteUnaligned(ref ptr, 42L);
                ptr = memory.Pointer;
                Equal("string1", Memory.ReadString(ref ptr));
                Equal("string2", Memory.ReadString(ref ptr));
                Equal(42L, Memory.ReadUnaligned<long>(ref ptr));
            }
        }

        [Fact]
        public static void CopyBlock()
        {
            char[] chars1 = new[] { 'a', 'b', 'c' };
            var chars2 = new char[2];
            Memory.Copy(ref chars1[1], ref chars2[0], 2);
            Equal('b', chars2[0]);
            Equal('c', chars2[1]);
        }

        [Fact]
        public static unsafe void CopyValue()
        {
            int a = 42, b = 0;
            Memory.Copy(&a, &b);
            Equal(a, b);
            Equal(42, b);
        }

        [Theory]
        [InlineData(-3893957)]
        [InlineData(int.MaxValue)]
        [InlineData(int.MinValue)]
        public static unsafe void PointerConversion(int value)
        {
            var ptr = new IntPtr(value).ToPointer<byte>();
            Equal(new IntPtr(value), new IntPtr(ptr));
        }

        [Fact]
        public static unsafe void ZeroMem()
        {
            var g = Guid.NewGuid();
            Memory.ClearBits(&g, sizeof(Guid));
            Equal(Guid.Empty, g);
        }

        [Fact]
        public static void ReadonlyRef()
        {
            var array = new[] { "a", "b", "c" };
            ref readonly var element = ref array.GetReadonlyRef(2);
            Equal("c", element);
        }
    }
}