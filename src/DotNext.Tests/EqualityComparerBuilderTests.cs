using System;
using Xunit;

namespace DotNext
{
    public sealed class EqualityComparerBuilderTests : Assert
    {
        private sealed unsafe class UnsafeClass
        {
            private readonly void* pointer;
            private readonly string str;
            public readonly IntPtr handle;

            internal UnsafeClass(IntPtr handle, string value)
            {
                this.handle = handle;
                pointer = handle.ToPointer();
                str = value;
            }
        }

        private unsafe struct UnsafeStruct
        {
            private readonly void* pointer;
            private readonly string str;
            public readonly IntPtr handle;

            internal UnsafeStruct(IntPtr handle, string value)
            {
                this.handle = handle;
                pointer = handle.ToPointer();
                str = value;
            }
        }

        private sealed class ComplexClass
        {
            private readonly UnsafeClass obj;
            private readonly UnsafeStruct[] array;

            internal ComplexClass(UnsafeClass obj, params UnsafeStruct[] array)
            {
                this.obj = obj;
                this.array = array;
            }

            public Guid Id { get; set; }
        }

        [Fact]
        public static void ClassWithInnerArrayComparer()
        {
            new EqualityComparerBuilder<ComplexClass>() { SaltedHashCode = true }.Build(out var equality, out var hashCode);
            var obj = new ComplexClass(null, new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null));
            True(equality(obj, new ComplexClass(null, new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null))));
            False(equality(obj, null));
        }

        [Fact]
        public static void StructArrayComparer()
        {
            new EqualityComparerBuilder<UnsafeStruct[]>().Build(out var equality, out var hashCode);
            var array = new[] { new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null) };
            Equal(hashCode(array), hashCode(new[] { new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null) }));
            True(equality(array, new[] { new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null) }));
        }

        [Fact]
        public static void UnsafeClassComparer()
        {
            new EqualityComparerBuilder<UnsafeClass>().Build(out var equality, out var hashCode);
            var obj = new UnsafeClass(new IntPtr(42L), "Hello, world!");
            Equal(hashCode(obj), hashCode(new UnsafeClass(new IntPtr(42L), "Hello, world!")));
            True(equality(obj, new UnsafeClass(new IntPtr(42L), "Hello, world!")));
        }

        [Fact]
        public static void ArrayOfRefTypeComparer()
        {
            new EqualityComparerBuilder<string[]>().Build(out var equality, out var hashCode);
            var array = new[] { "a", "b" };
            Equal(hashCode(array), hashCode(new[] { "a", "b" }));
            True(equality(array, new[] { "a", "b" }));
        }

        [Fact]
        public static void LongEqualityComparer()
        {
            new EqualityComparerBuilder<long>().Build(out var equality, out var hashCode);
            Equal(hashCode(42L), hashCode(42L));
            True(equality(42L, 42L));
        }

        [Fact]
        public static void GuidEqualityComparer()
        {
            new EqualityComparerBuilder<Guid>().Build(out var equality, out var hashCode);
            var g = Guid.NewGuid();
            Equal(hashCode(g), hashCode(g));
            True(equality(g, g));
        }
    }
}
