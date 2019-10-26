using System;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace DotNext.Threading
{
    [ExcludeFromCodeCoverage]
    public sealed class AtomicTests : Assert
    {
        [Fact]
        public static void AtomicArrayTest()
        {
            var array = new[] { "a", "b" };
            array.UpdateAndGet(1, s => s + "c");
            Equal("bc", array.VolatileRead(1));
        }

        [Fact]
        public static void AtomicFloatTest()
        {
            float i = 10F;
            Equal(11F, i.IncrementAndGet());
            Equal(10F, i.DecrementAndGet());
            i = 20F;
            True(i.CompareAndSet(20F, 30F));
            Equal(30F, i);
            False(i.CompareAndSet(20F, 50F));
            Equal(30F, i);
            Equal(80F, i.Add(50F));
            Equal(80F, i);
            Equal(80F, i.GetAndAccumulate(10F, (x, y) => x + y));
            Equal(90F, i);
            Equal(10F, i.AccumulateAndGet(80F, (x, y) => x - y));
            Equal(10F, i);
            Equal(10F, i.GetAndSet(25F));
            Equal(25F, i);
            Equal(42F, i.UpdateAndGet(current => 42F));
            Equal(42F, i.GetAndUpdate(current => 52F));
            Equal(52F, i);
        }

        [Fact]
        public static void AtomicDoubleTest()
        {
            double i = 10D;
            Equal(11D, i.IncrementAndGet());
            Equal(10D, i.DecrementAndGet());
            i = 20D;
            True(i.CompareAndSet(20D, 30D));
            Equal(30D, i);
            False(i.CompareAndSet(20D, 50D));
            Equal(30D, i);
            Equal(80D, i.Add(50D));
            Equal(80D, i);
            Equal(80D, i.GetAndAccumulate(10D, (x, y) => x + y));
            Equal(90D, i);
            Equal(10D, i.AccumulateAndGet(80D, (x, y) => x - y));
            Equal(10D, i);
            Equal(10D, i.GetAndSet(25D));
            Equal(25D, i);
            Equal(42D, i.UpdateAndGet(current => 42D));
            Equal(42D, i.GetAndUpdate(current => 52D));
            Equal(52D, i);
        }

        [Fact]
        public static void AtomicIntTest()
        {
            var i = 10;
            Equal(11, i.IncrementAndGet());
            Equal(10, i.DecrementAndGet());
            i = 20;
            True(i.CompareAndSet(20, 30));
            Equal(30, i);
            False(i.CompareAndSet(20, 50));
            Equal(30, i);
            Equal(80, i.Add(50));
            Equal(80, i);
            Equal(80, i.GetAndAccumulate(10, (x, y) => x + y));
            Equal(90, i);
            Equal(10, i.AccumulateAndGet(80, (x, y) => x - y));
            Equal(10, i);
            Equal(10, i.GetAndSet(25));
            Equal(25, i);
            Equal(42, i.UpdateAndGet(current => 42));
            Equal(42, i.GetAndUpdate(current => 52));
            Equal(52, i);
        }

        [Fact]
        public static void AtomicLongTest()
        {
            var i = 10L;
            Equal(11L, i.IncrementAndGet());
            Equal(10L, i.DecrementAndGet());
            i = 20L;
            True(i.CompareAndSet(20L, 30L));
            Equal(30L, i);
            False(i.CompareAndSet(20L, 50L));
            Equal(30L, i);
            Equal(80L, i.Add(50L));
            Equal(80L, i);
            Equal(80L, i.GetAndAccumulate(10L, (x, y) => x + y));
            Equal(90L, i);
            Equal(10L, i.AccumulateAndGet(80L, (x, y) => x - y));
            Equal(10L, i);
            Equal(10L, i.GetAndSet(25L));
            Equal(25L, i);
            Equal(42L, i.UpdateAndGet(current => 42L));
            Equal(42L, i.GetAndUpdate(current => 52L));
            Equal(52L, i);
        }

        [Fact]
        public static void AtomicReferenceTest()
        {
            var stref = new AtomicReference<string>("");
            Equal("", stref.Value);
            Empty(stref.GetAndSet(null));
            Null(stref.Value);
            NotEmpty(stref.SetAndGet("Hello"));
            Equal("Hello, world!", stref.AccumulateAndGet(", world!", (x, y) => x + y));
            Equal("Hello, world!", stref.Value);
            Equal("Hello, world!", stref.GetAndUpdate(current => ""));
            Empty(stref.Value);
            stref.Value = null;
            Equal("Hello", stref.SetIfNull(() => "Hello"));
            Equal("Hello", stref.SetIfNull(() => ""));
            Equal("Hello", stref.Value);
        }

        [Fact]
        public static void AtomicBooleanTest()
        {
            var value = new AtomicBoolean(false);
            True(value.FalseToTrue());
            False(value.FalseToTrue());
            True(value.TrueToFalse());
            False(value.TrueToFalse());
            True(value.NegateAndGet());
            True(value.GetAndNegate());
            False(value.Value);
            value.Value = true;
            True(value.Value);
        }

        [Fact]
        public static void AtomicEnumTest()
        {
            var value = new AtomicEnum<EnvironmentVariableTarget>();
            Equal(EnvironmentVariableTarget.Process, value.Value);
            Equal(EnvironmentVariableTarget.Process, value.GetAndSet(EnvironmentVariableTarget.Machine));
            Equal(EnvironmentVariableTarget.Machine, value.Value);
            Equal(EnvironmentVariableTarget.Machine, value.GetAndUpdate(x => EnvironmentVariableTarget.User));
            Equal(EnvironmentVariableTarget.User, value.Value);
        }

        [Fact]
        public static void AtomicIntPtrTest()
        {
            var i = new IntPtr(10);
            Equal(new IntPtr(11), i.IncrementAndGet());
            Equal(new IntPtr(10), i.DecrementAndGet());
            i = new IntPtr(20);
            True(i.CompareAndSet(new IntPtr(20), new IntPtr(30)));
            Equal(new IntPtr(30), i);
            False(i.CompareAndSet(new IntPtr(20), new IntPtr(50)));
            Equal(new IntPtr(30), i);
            Equal(new IntPtr(30), i.GetAndAccumulate(new IntPtr(60), (x, y) => x + y.ToInt32()));
            Equal(new IntPtr(90), i);
            Equal(new IntPtr(10), i.AccumulateAndGet(new IntPtr(80), (x, y) => x - y.ToInt32()));
            Equal(new IntPtr(10), i);
            Equal(new IntPtr(10), i.GetAndSet(new IntPtr(25)));
            Equal(new IntPtr(25), i);
            Equal(new IntPtr(42), i.UpdateAndGet(current => new IntPtr(42)));
            Equal(new IntPtr(42), i.GetAndUpdate(current => new IntPtr(52)));
            Equal(new IntPtr(52), i);
        }

        private enum LongEnum : long
        {
            One = 0L,
            Two = 1L
        }

        private enum ByteEnum : byte
        {
            One = 0,
            Two = 1
        }

        [Fact]
        public static void EnumVolatileReadWrite()
        {
            var be = ByteEnum.Two;
            Equal(ByteEnum.Two, be.VolatileRead());
            be.VolatileWrite(ByteEnum.One);
            Equal(ByteEnum.One, be);

            var le = LongEnum.Two;
            Equal(LongEnum.Two, le.VolatileRead());
            le.VolatileWrite(LongEnum.One);
            Equal(LongEnum.One, le);
        }
    }
}
