using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
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
        public unsafe static void RefToTypedReference()
        {
            var reference = default(TypedReference);
            var i = 20;
            Memory.AsTypedReference(ref i, &reference);
            Equal(20, TypedReference.ToObject(reference));
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
        public unsafe static void SwapValuesByPointer()
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
        public static void UnboxRef()
        {
            object obj = new Guid();
            Equal(Guid.Empty, obj);
            ref var boxed = ref Memory.GetBoxedValue<Guid>(obj);
            boxed = Guid.NewGuid();
            NotEqual(Guid.Empty, obj);
        }

        [Fact]
        public unsafe static void BitwiseEqualityForByte()
        {
            byte value1 = 10;
            byte value2 = 20;
            False(Memory.Equals(&value1, &value2, sizeof(byte)));
            value2 = 10;
            True(Memory.Equals(&value1, &value2, sizeof(byte)));
        }

        [Fact]
        public unsafe static void BitwiseEqualityForLong()
        {
            var value1 = 10L;
            var value2 = 20L;
            False(Memory.Equals(&value1, &value2, sizeof(long)));
            value2 = 10;
            True(Memory.Equals(&value1, &value2, sizeof(long)));
        }
    }
}