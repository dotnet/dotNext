using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[ExcludeFromCodeCoverage]
public sealed class AtomicTests : Test
{
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
        Equal(80F, i.AddAndGet(50F));
        Equal(80F, i);
        Equal(80F, i.GetAndAccumulate(10F, static (x, y) => x + y));
        Equal(90F, i);
        Equal(10F, i.AccumulateAndGet(80F, static (x, y) => x - y));
        Equal(10F, i);
        Equal(10F, i.GetAndSet(25F));
        Equal(25F, i);
        Equal(42F, i.UpdateAndGet(static current => 42F));
        Equal(42F, i.GetAndUpdate(static current => 52F));
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
        Equal(80D, i.AddAndGet(50D));
        Equal(80D, i);
        Equal(80D, i.GetAndAccumulate(10D, static (x, y) => x + y));
        Equal(90D, i);
        Equal(10D, i.AccumulateAndGet(80D, static (x, y) => x - y));
        Equal(10D, i);
        Equal(10D, i.GetAndSet(25D));
        Equal(25D, i);
        Equal(42D, i.UpdateAndGet(static current => 42D));
        Equal(42D, i.GetAndUpdate(static current => 52D));
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
        Equal(80, i.AddAndGet(50));
        Equal(80, i);
        Equal(80, i.GetAndAccumulate(10, static (x, y) => x + y));
        Equal(90, i);
        Equal(10, i.AccumulateAndGet(80, static (x, y) => x - y));
        Equal(10, i);
        Equal(10, i.GetAndSet(25));
        Equal(25, i);
        Equal(42, i.UpdateAndGet(static current => 42));
        Equal(42, i.GetAndUpdate(static current => 52));
        Equal(52, i);

        Equal(52, i.GetAndBitwiseOr(1));
        Equal(53, i);
        i = 52;
        Equal(53, i.BitwiseOrAndGet(1));

        Equal(53, i.GetAndBitwiseAnd(1 << 2));
        Equal(4, i);
        i = 53;
        Equal(4, i.BitwiseAndAndGet(1 << 2));

        Equal(4, i.GetAndBitwiseXor(3));
        Equal(7, i);
        i = 4;
        Equal(7, i.BitwiseXorAndGet(3));
    }

    [Fact]
    public static void AtomicUIntTest()
    {
        uint i = 10U;
        Equal(11U, i.IncrementAndGet());
        Equal(10U, i.DecrementAndGet());
        i = 20U;
        True(i.CompareAndSet(20U, 30U));
        Equal(30U, i);
        False(i.CompareAndSet(20U, 50U));
        Equal(30U, i);
        Equal(80U, i.AddAndGet(50U));
        Equal(80U, i);
        Equal(80U, i.GetAndAccumulate(10, static (x, y) => x + y));
        Equal(90U, i);
        Equal(10U, i.AccumulateAndGet(80, static (x, y) => x - y));
        Equal(10U, i);
        Equal(10U, i.GetAndSet(25U));
        Equal(25U, i);
        Equal(42U, i.UpdateAndGet(static current => 42U));
        Equal(42U, i.GetAndUpdate(static current => 52U));
        Equal(52U, i);

        Equal(52U, i.GetAndBitwiseOr(1U));
        Equal(53U, i);
        i = 52U;
        Equal(53U, i.BitwiseOrAndGet(1U));

        Equal(53U, i.GetAndBitwiseAnd(1U << 2));
        Equal(4U, i);
        i = 53U;
        Equal(4U, i.BitwiseAndAndGet(1U << 2));

        Equal(4U, i.GetAndBitwiseXor(3U));
        Equal(7U, i);
        i = 4U;
        Equal(7U, i.BitwiseXorAndGet(3U));
    }

    [Fact]
    public static void AtomicULongTest()
    {
        ulong i = 10UL;
        Equal(11UL, i.IncrementAndGet());
        Equal(10UL, i.DecrementAndGet());
        i = 20UL;
        True(i.CompareAndSet(20UL, 30UL));
        Equal(30UL, i);
        False(i.CompareAndSet(20UL, 50UL));
        Equal(30UL, i);
        Equal(80UL, i.AddAndGet(50UL));
        Equal(80UL, i);
        Equal(80UL, i.GetAndAccumulate(10UL, static (x, y) => x + y));
        Equal(90UL, i);
        Equal(10UL, i.AccumulateAndGet(80UL, static (x, y) => x - y));
        Equal(10UL, i);
        Equal(10UL, i.GetAndSet(25UL));
        Equal(25UL, i);
        Equal(42UL, i.UpdateAndGet(static current => 42UL));
        Equal(42UL, i.GetAndUpdate(static current => 52UL));
        Equal(52UL, i);

        Equal(52UL, i.GetAndBitwiseOr(1UL));
        Equal(53UL, i);
        i = 52UL;
        Equal(53UL, i.BitwiseOrAndGet(1UL));

        Equal(53UL, i.GetAndBitwiseAnd(1UL << 2));
        Equal(4UL, i);
        i = 53;
        Equal(4UL, i.BitwiseAndAndGet(1UL << 2));

        Equal(4UL, i.GetAndBitwiseXor(3UL));
        Equal(7UL, i);
        i = 4UL;
        Equal(7UL, i.BitwiseXorAndGet(3UL));
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
        Equal(80L, i.AddAndGet(50L));
        Equal(80L, i);
        Equal(80L, i.GetAndAccumulate(10L, static (x, y) => x + y));
        Equal(90L, i);
        Equal(10L, i.AccumulateAndGet(80L, static (x, y) => x - y));
        Equal(10L, i);
        Equal(10L, i.GetAndSet(25L));
        Equal(25L, i);
        Equal(42L, i.UpdateAndGet(static current => 42L));
        Equal(42L, i.GetAndUpdate(static current => 52L));
        Equal(52L, i);

        Equal(52L, i.GetAndBitwiseOr(1L));
        Equal(53L, i);
        i = 52L;
        Equal(53L, i.BitwiseOrAndGet(1L));

        Equal(53L, i.GetAndBitwiseAnd(1L << 2));
        Equal(4L, i);
        i = 53L;
        Equal(4L, i.BitwiseAndAndGet(1L << 2));

        Equal(4L, i.GetAndBitwiseXor(3L));
        Equal(7L, i);
        i = 4L;
        Equal(7L, i.BitwiseXorAndGet(3L));
    }

    [Fact]
    public static void AtomicReferenceTest()
    {
        var stref = "Hello";
        Equal("Hello, world!", AtomicReference.AccumulateAndGet<string>(ref stref, ", world!", static (x, y) => x + y));
        Equal("Hello, world!", stref);
        Equal("Hello, world!", AtomicReference.GetAndUpdate(ref stref, static current => ""));
        Empty(stref);
    }

    [Fact]
    public static void AtomicBooleanTest()
    {
        var value = new AtomicBoolean(false);
        False(value.Equals(true));
        True(value.Equals(false));
        True(value.FalseToTrue());
        False(value.FalseToTrue());
        True(value.TrueToFalse());
        False(value.TrueToFalse());
        True(value.NegateAndGet());
        True(value.GetAndNegate());
        False(value.Value);
        value.Value = true;
        True(value.Value);
        True(value.GetAndSet(false));
        False(value.Value);
        True(value.SetAndGet(true));
        True(value.Value);
        Equal(bool.TrueString, value.ToString());
        True(value.GetAndAccumulate(false, static (current, update) =>
        {
            True(current);
            False(update);
            return current & update;
        }));
        False(value.Value);
        True(value.AccumulateAndGet(true, static (current, update) => current | update));
        True(value.Value);
        True(value.GetAndUpdate(static x => !x));
        False(value.Value);
        True(value.UpdateAndGet(static x => !x));
        True(value.Value);
    }

    [Fact]
    public static void AtomicEnumTest()
    {
        var value = new AtomicEnum<EnvironmentVariableTarget>();
        True(value.Equals(EnvironmentVariableTarget.Process));
        False(value.Equals(EnvironmentVariableTarget.User));
        Equal(EnvironmentVariableTarget.Process, value.Value);
        Equal(EnvironmentVariableTarget.Process, value.GetAndSet(EnvironmentVariableTarget.Machine));
        Equal(EnvironmentVariableTarget.Machine, value.Value);
        Equal(EnvironmentVariableTarget.Machine, value.GetAndUpdate(static x => EnvironmentVariableTarget.User));
        Equal(EnvironmentVariableTarget.User, value.Value);
        Equal(EnvironmentVariableTarget.User.ToString(), value.ToString());
        value.Value = EnvironmentVariableTarget.Process;
        Equal(EnvironmentVariableTarget.Machine, value.SetAndGet(EnvironmentVariableTarget.Machine));
        Equal(EnvironmentVariableTarget.Machine, value.Value);
        Equal(EnvironmentVariableTarget.User, value.UpdateAndGet(static x =>
        {
            Equal(EnvironmentVariableTarget.Machine, x);
            return EnvironmentVariableTarget.User;
        }));
        value.Value = EnvironmentVariableTarget.Process;
        Equal(EnvironmentVariableTarget.Process, value.GetAndAccumulate(EnvironmentVariableTarget.User, static (current, update) =>
        {
            Equal(EnvironmentVariableTarget.Process, current);
            Equal(EnvironmentVariableTarget.User, update);
            return (int)current + update;
        }));
        Equal(EnvironmentVariableTarget.User, value.Value);
        value.Value = EnvironmentVariableTarget.Process;
        Equal(EnvironmentVariableTarget.User, value.AccumulateAndGet(EnvironmentVariableTarget.User, static (current, update) =>
        {
            Equal(EnvironmentVariableTarget.Process, current);
            Equal(EnvironmentVariableTarget.User, update);
            return (int)current + update;
        }));
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
        Equal(new IntPtr(30), i.GetAndAccumulate(new IntPtr(60), static (x, y) => x + y.ToInt32()));
        Equal(new IntPtr(90), i);
        Equal(new IntPtr(10), i.AccumulateAndGet(new IntPtr(80), static (x, y) => x - y.ToInt32()));
        Equal(new IntPtr(10), i);
        Equal(new IntPtr(10), i.GetAndSet(new IntPtr(25)));
        Equal(new IntPtr(25), i);
        Equal(new IntPtr(42), i.UpdateAndGet(static current => new IntPtr(42)));
        Equal(new IntPtr(42), i.GetAndUpdate(static current => new IntPtr(52)));
        Equal(new IntPtr(52), i);

        Equal((IntPtr)52, i.GetAndBitwiseOr((IntPtr)1));
        Equal((IntPtr)53, i);
        i = (IntPtr)52;
        Equal((IntPtr)53, i.BitwiseOrAndGet((IntPtr)1));

        Equal((IntPtr)53, i.GetAndBitwiseAnd((nint)1 << 2));
        Equal((IntPtr)4, i);
        i = (IntPtr)53;
        Equal((IntPtr)4, i.BitwiseAndAndGet((nint)1 << 2));

        Equal((IntPtr)4, i.GetAndBitwiseXor((IntPtr)3));
        Equal((IntPtr)7, i);
        i = (IntPtr)4;
        Equal((IntPtr)7, i.BitwiseXorAndGet((IntPtr)3));
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