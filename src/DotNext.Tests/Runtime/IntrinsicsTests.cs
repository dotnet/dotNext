using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using Xunit;

namespace DotNext.Runtime
{
    [ExcludeFromCodeCoverage]
    public class IntrinsicsTests : Test
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
            True(Intrinsics.AreSame(in field, in g));
        }

        [Fact]
        public void FieldTypedReferenceClass()
        {
            TypedReference reference = __makeref(str);
            ref string f = ref reference.AsRef<string>();
            Null(f);
            f = "Hello, world!";
            Equal(str, f);
            True(Intrinsics.AreSame(in str, in f));
        }

        [Fact]
        public static void SwapValues()
        {
            var x = 10;
            var y = 20;
            Intrinsics.Swap(ref x, ref y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public static unsafe void SwapValuesByPointer()
        {
            var x = 10;
            var y = 20;
            Intrinsics.Swap(&x, &y);
            Equal(20, x);
            Equal(10, y);
        }

        [Fact]
        public static void AddressOfLocal()
        {
            var i = 20;
            True(Intrinsics.AddressOf(i) != IntPtr.Zero);
        }

        [Fact]
        public static unsafe void BitwiseEqualityForByte()
        {
            byte value1 = 10;
            byte value2 = 20;
            False(Intrinsics.Equals(&value1, &value2, sizeof(byte)));
            value2 = 10;
            True(Intrinsics.Equals(&value1, &value2, sizeof(byte)));
        }

        [Fact]
        public static unsafe void BitwiseEqualityForLong()
        {
            var value1 = 10L;
            var value2 = 20L;
            False(Intrinsics.Equals(&value1, &value2, sizeof(long)));
            value2 = 10;
            True(Intrinsics.Equals(&value1, &value2, sizeof(long)));
        }

        [Fact]
        public static unsafe void BitwiseHashCode()
        {
            var i = 42L;
            NotEqual(0, Intrinsics.GetHashCode32(&i, sizeof(long)));
            NotEqual(0L, Intrinsics.GetHashCode64(&i, sizeof(long)));
        }

        [Fact]
        public static void CopyBlock()
        {
            char[] chars1 = new[] { 'a', 'b', 'c' };
            var chars2 = new char[2];
            Intrinsics.Copy(in chars1[1], out chars2[0], 2);
            Equal('b', chars2[0]);
            Equal('c', chars2[1]);
        }

        [Fact]
        public static unsafe void CopyValue()
        {
            int a = 42, b = 0;
            Intrinsics.Copy(&a, &b);
            Equal(a, b);
            Equal(42, b);
        }

        [Fact]
        public static unsafe void ZeroMem()
        {
            var g = Guid.NewGuid();
            Intrinsics.ClearBits(&g, sizeof(Guid));
            Equal(Guid.Empty, g);
        }

        [Fact]
        public static void ReadonlyRef2()
        {
            var array = new[] { "a", "b", "c" };
            ref readonly var element = ref array.GetReadonlyRef<string, ICloneable>(2);
            Equal("c", element.Clone());
        }

        [Fact]
        public static void IsNullable()
        {
            True(Intrinsics.IsNullable<string>());
            True(Intrinsics.IsNullable<ValueType>());
            True(Intrinsics.IsNullable<int?>());
            False(Intrinsics.IsNullable<int>());
            False(Intrinsics.IsNullable<IntPtr>());
        }

        [Fact]
        public static void RefTypeDefaultTest()
        {
            True(Intrinsics.IsDefault<string>(default));
            False(Intrinsics.IsDefault(""));
        }

        [Fact]
        public static void StructTypeDefaultTest()
        {
            var value = default(Guid);
            True(Intrinsics.IsDefault(value));
            value = Guid.NewGuid();
            False(Intrinsics.IsDefault(value));
        }

        [Fact]
        public static void SmallStructDefault()
        {
            True(Intrinsics.IsDefault(default(long)));
            False(Intrinsics.IsDefault(42L));
            True(Intrinsics.IsDefault(default(int)));
            False(Intrinsics.IsDefault(42));
            True(Intrinsics.IsDefault(default(byte)));
            False(Intrinsics.IsDefault((byte)42));
            True(Intrinsics.IsDefault(default(short)));
            False(Intrinsics.IsDefault((short)42));
        }

        [Fact]
        public static void Bitcast()
        {
            var point = new Point { X = 40, Y = 100 };
            Intrinsics.Bitcast(point, out decimal dec);
            Intrinsics.Bitcast(dec, out point);
            Equal(40, point.X);
            Equal(100, point.Y);
            Intrinsics.Bitcast<uint, int>(2U, out var i);
            Equal(2, i);
        }

        [Fact]
        public static void BitcastToLargerValueType()
        {
            var point = new Point { X = 40, Y = 100 };
            Intrinsics.Bitcast(point, out Guid g);
            False(g == Guid.Empty);
        }

        [Fact]
        public static void LightweightTypeOf()
        {
            var handle = Intrinsics.TypeOf<string>();
            Equal(typeof(string).TypeHandle, handle);
            NotEqual(typeof(StringComparer).TypeHandle, handle);
        }

        [Flags]
        private enum ByteEnum : byte
        {
            None = 0,
            One = 1,
            Two = 2,
        }

        [Flags]
        private enum ShortEnum : short
        {
            None = 0,
            One = 1,
            Two = 2,
        }

        [Flags]
        private enum LongEnum : long
        {
            None = 0L,
            One = 1L,
            Two = 2L,
        }

        [Fact]
        public static void HasFlag()
        {
            static void HasFlag<T>(T value, T validFlag, T invalidFlag)
                where T : struct, Enum
            {
                True(Intrinsics.HasFlag(value, validFlag));
                False(Intrinsics.HasFlag(value, invalidFlag));
            }

            HasFlag(BindingFlags.Public | BindingFlags.Instance, BindingFlags.Public, BindingFlags.Static);
            HasFlag(ByteEnum.Two, ByteEnum.Two, ByteEnum.One);
            HasFlag(ShortEnum.Two, ShortEnum.Two, ShortEnum.One);
            HasFlag(LongEnum.Two, LongEnum.Two, LongEnum.One);
        }

        [Fact]
        public static void CastObject()
        {
            Throws<InvalidCastException>(() => Intrinsics.Cast<string>(null));
            Throws<InvalidCastException>(() => Intrinsics.Cast<string>(20));
            Throws<InvalidCastException>(() => Intrinsics.Cast<int?>(null));
            Equal(42, Intrinsics.Cast<int?>(42));
        }

        [Fact]
        public static void NullUnsafeCast()
        {
            var castMethod = typeof(Intrinsics).GetMethod("NullAwareCast", BindingFlags.DeclaredOnly | BindingFlags.Static | BindingFlags.NonPublic);
            var objToStr = castMethod.MakeGenericMethod(typeof(string)).CreateDelegate<Func<object, string>>();
            Null(objToStr(null));
            Equal(string.Empty, objToStr(""));

            var objToNullable = castMethod.MakeGenericMethod(typeof(int?)).CreateDelegate<Func<object, int?>>();
            Null(objToNullable(null));
            Equal(53, objToNullable(53));

            var objToInt = castMethod.MakeGenericMethod(typeof(int)).CreateDelegate<Func<object, int>>();
            Equal(53, objToInt(53));

            Throws<InvalidCastException>(() => objToStr(54));
            Throws<InvalidCastException>(() => objToNullable(string.Empty));
            Throws<InvalidCastException>(() => objToInt(string.Empty));
            Throws<InvalidCastException>(() => objToInt(null));
        }

        [Fact]
        public static void ExactTypeCheck()
        {
            object obj = 12;
            True(Intrinsics.IsExactTypeOf<int>(obj));
            False(Intrinsics.IsExactTypeOf<long>(obj));
            obj = string.Empty;
            True(Intrinsics.IsExactTypeOf<string>(obj));
            False(Intrinsics.IsExactTypeOf<object>(obj));
            obj = Array.Empty<string>();
            True(Intrinsics.IsExactTypeOf<string[]>(obj));
            False(Intrinsics.IsExactTypeOf<object[]>(obj));
        }

        [Fact]
        public static void ThrowObjectAsException()
        {
            Throws<InvalidOperationException>(() => Intrinsics.Throw(new InvalidOperationException()));
            var e = Throws<RuntimeWrappedException>(() => Intrinsics.Throw("String"));
            Equal("String", e.WrappedException);
            Throws<InvalidOperationException>(new Action(() => throw Intrinsics.Error(new InvalidOperationException())));
            e = Throws<RuntimeWrappedException>(new Action(() => throw Intrinsics.Error("String")));
            Equal("String", e.WrappedException);
        }

        [Fact]
        public static void ObjectClone()
        {
            var obj = new IntrinsicsTests();
            var obj2 = Intrinsics.ShallowCopy(obj);
            obj.field = Guid.NewGuid();
            obj2.str = string.Empty;
            NotEqual(obj.field, obj2.field);
            NotEqual(obj.str, obj2.str);
            NotSame(obj, obj2);
        }

        [Fact]
        public static void ArrayLength()
        {
            int[] array = { 42 };
            Equal(new IntPtr(1), (IntPtr)Intrinsics.GetLength(array));
            array = Array.Empty<int>();
            Equal(default, Intrinsics.GetLength(array));
            Equal(new IntPtr(4), (IntPtr)Intrinsics.GetLength(new int[2, 2]));
        }

        [Fact]
        [Obsolete("This test is for checking obsolete member")]
        public static void ArrayElement()
        {
            string[] array = { "42" };
            var element = Intrinsics.GetElement<IEquatable<string>>(array, IntPtr.Zero);
            True(element.Equals("42"));

            Intrinsics.GetElementReference(array, IntPtr.Zero) = "43";
            Equal("43", array[0]);

            element = Intrinsics.GetElement<IEquatable<string>>(array, UIntPtr.Zero);
            True(element.Equals("43"));

            Intrinsics.GetElementReference(array, UIntPtr.Zero) = "44";
            Equal("44", array[0]);
        }

        [Fact]
        public static void CombineBits()
        {
            Equal(0, Intrinsics.ToInt32(false, false));
            Equal(1, Intrinsics.ToInt32(true, false));
            Equal(2, Intrinsics.ToInt32(false, true));
            Equal(3, Intrinsics.ToInt32(true, true));
        }
    }
}