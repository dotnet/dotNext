using System;
using Xunit;

namespace DotNext.Runtime.InteropServices
{
    using static Memory;

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
            True(AreSame(in field, in g));
        }

        [Fact]
        public void FieldTypedReferenceClass()
        {
            TypedReference reference = __makeref(str);
            ref string f = ref reference.AsRef<string>();
            Null(f);
            f = "Hello, world!";
            Equal(str, f);
            True(AreSame(in str, in f));
        }

        [Fact]
        public unsafe static void RefToTypedReference()
        {
            var reference = default(TypedReference);
            var i = 20;
            AsTypedReference(ref i, &reference);
            Equal(20, TypedReference.ToObject(reference));
        }

        [Fact]
        public static void SwapValues()
        {
            var x = 10;
            var y = 20;
            Swap(ref x, ref y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public unsafe static void SwapValuesByPointer()
        {
            var x = 10;
            var y = 20;
            Swap(&x, &y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public static void AddressOfLocal()
        {
            var i = 20;
            True(AddressOf(i) != IntPtr.Zero);
        }
    }
}