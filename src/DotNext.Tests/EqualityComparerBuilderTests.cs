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
            var hashCode = EqualityComparerBuilder.BuildGetHashCode<ComplexClass>();
            var equality = EqualityComparerBuilder.BuildEquals<ComplexClass>();
            var obj = new ComplexClass(null, new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null));
            True(equality(obj, new ComplexClass(null, new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null))));
            False(equality(obj, null));
        }

        [Fact]
        public static void StructArrayComparer()
        {
            var hashCode = EqualityComparerBuilder.BuildGetHashCode<UnsafeStruct[]>();
            var equality = EqualityComparerBuilder.BuildEquals<UnsafeStruct[]>();
            var array = new[] { new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null) };
            Equal(hashCode(array), hashCode(new[] { new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null) }));
            True(equality(array, new[] { new UnsafeStruct(new IntPtr(42L), "Hello, world!"), new UnsafeStruct(new IntPtr(43L), null) }));
        }

        [Fact]
        public static void UnsafeClassComparer()
        {
            var hashCode = EqualityComparerBuilder.BuildGetHashCode<UnsafeClass>();
            var equality = EqualityComparerBuilder.BuildEquals<UnsafeClass>();
            var obj = new UnsafeClass(new IntPtr(42L), "Hello, world!");
            Equal(hashCode(obj), hashCode(new UnsafeClass(new IntPtr(42L), "Hello, world!")));
            True(equality(obj, new UnsafeClass(new IntPtr(42L), "Hello, world!")));
        }

        [Fact]
        public static void ArrayOfRefTypeComparer()
        {
            var hashCode = EqualityComparerBuilder.BuildGetHashCode<string[]>();
            var equality = EqualityComparerBuilder.BuildEquals<string[]>();
            var array = new string[] { "a", "b" };
            Equal(hashCode(array), hashCode(new string[] { "a", "b" }));
            True(equality(array, new string[] { "a", "b" }));
        }

        [Fact]
        public static void LongEqualityComparer()
        {
            var hashCode = EqualityComparerBuilder.BuildGetHashCode<long>();
            var equality = EqualityComparerBuilder.BuildEquals<long>();
            Equal(hashCode(42L), hashCode(42L));
            True(equality(42L, 42L));
        }

        [Fact]
        public static void GuidEqualityComparer()
        {
            var hashCode = EqualityComparerBuilder.BuildGetHashCode<Guid>();
            var equality = EqualityComparerBuilder.BuildEquals<Guid>();
            var g = Guid.NewGuid();
            Equal(hashCode(g), hashCode(g));
            True(equality(g, g));
        }
    }
}
