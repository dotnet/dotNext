namespace DotNext.Threading;

public sealed class AtomicTests : Test
{
    [Fact]
    public static void AtomicFloatTest()
    {
        float i = 10;
        Equal(80, Atomic.GetAndAccumulate(ref i, 10, static (x, y) => x + y));
        Equal(90, i);
        Equal(10, Atomic.AccumulateAndGet(ref i, 80, static (x, y) => x - y));
        Equal(10, i);

        Equal(42, Atomic.UpdateAndGet(ref i, static current => 42));
        Equal(42, Atomic.GetAndUpdate(ref i, static current => 52));
        Equal(52, i);
    }

    [Fact]
    public static void AtomicDoubleTest()
    {
        double i = 10;
        Equal(80, Atomic.GetAndAccumulate(ref i, 10, static (x, y) => x + y));
        Equal(90, i);
        Equal(10, Atomic.AccumulateAndGet(ref i, 80, static (x, y) => x - y));
        Equal(10, i);

        Equal(42, Atomic.UpdateAndGet(ref i, static current => 42));
        Equal(42, Atomic.GetAndUpdate(ref i, static current => 52));
        Equal(52, i);
    }

    [Fact]
    public static void AtomicIntTest()
    {
        var i = 10;
        Equal(80, Atomic.GetAndAccumulate(ref i, 10, static (x, y) => x + y));
        Equal(90, i);
        Equal(10, Atomic.AccumulateAndGet(ref i, 80, static (x, y) => x - y));
        Equal(10, i);

        Equal(42, Atomic.UpdateAndGet(ref i, static current => 42));
        Equal(42, Atomic.GetAndUpdate(ref i, static current => 52));
        Equal(52, i);
    }

    [Fact]
    public static void AtomicUIntTest()
    {
        uint i = 10U;
        Equal(80U, Atomic.GetAndAccumulate(ref i, 10U, static (x, y) => x + y));
        Equal(90U, i);
        Equal(10U, Atomic.AccumulateAndGet(ref i, 80U, static (x, y) => x - y));
        Equal(10U, i);

        Equal(42U, Atomic.UpdateAndGet(ref i, static current => 42U));
        Equal(42U, Atomic.GetAndUpdate(ref i, static current => 52U));
        Equal(52U, i);
    }

    [Fact]
    public static void AtomicULongTest()
    {
        var i = 10UL;
        Equal(80UL, Atomic.GetAndAccumulate(ref i, 10UL, static (x, y) => x + y));
        Equal(90UL, i);
        Equal(10UL, Atomic.AccumulateAndGet(ref i, 80UL, static (x, y) => x - y));
        Equal(10UL, i);

        Equal(42UL, Atomic.UpdateAndGet(ref i, static current => 42UL));
        Equal(42UL, Atomic.GetAndUpdate(ref i, static current => 52UL));
        Equal(52UL, i);
    }

    [Fact]
    public static void AtomicLongTest()
    {
        var i = 10L;
        Equal(80, Atomic.GetAndAccumulate(ref i, 10, static (x, y) => x + y));
        Equal(90, i);
        Equal(10, Atomic.AccumulateAndGet(ref i, 80, static (x, y) => x - y));
        Equal(10, i);

        Equal(42, Atomic.UpdateAndGet(ref i, static current => 42));
        Equal(42, Atomic.GetAndUpdate(ref i, static current => 52));
        Equal(52, i);
    }

    [Fact]
    public static void AtomicBooleanTest()
    {
        var value = new Atomic.Boolean(false);
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
    public static void AtomicIntPtrTest()
    {
        nint i = 10;
        Equal(80, Atomic.GetAndAccumulate(ref i, 10, static (x, y) => x + y));
        Equal(90, i);
        Equal(10, Atomic.AccumulateAndGet(ref i, 80, static (x, y) => x - y));
        Equal(10, i);

        Equal(42, Atomic.UpdateAndGet(ref i, static current => 42));
        Equal(42, Atomic.GetAndUpdate(ref i, static current => 52));
        Equal(52, i);
    }

    [Fact]
    public static void AtomicUIntPtrTest()
    {
        nuint i = 10U;
        Equal(80U, Atomic.GetAndAccumulate(ref i, 10U, static (x, y) => x + y));
        Equal(90U, i);
        Equal(10U, Atomic.AccumulateAndGet(ref i, 80U, static (x, y) => x - y));
        Equal(10U, i);

        Equal(42U, Atomic.UpdateAndGet(ref i, static current => 42U));
        Equal(42U, Atomic.GetAndUpdate(ref i, static current => 52U));
        Equal(52U, i);
    }

    [Fact]
    public static void IsAtomicWrite()
    {
        True(Atomic.IsAtomic<byte>());
        True(Atomic.IsAtomic<sbyte>());
        True(Atomic.IsAtomic<bool>());
        True(Atomic.IsAtomic<short>());
        True(Atomic.IsAtomic<ushort>());
        True(Atomic.IsAtomic<int>());
        True(Atomic.IsAtomic<uint>());
        True(Atomic.IsAtomic<long>());
        True(Atomic.IsAtomic<ulong>());
        True(Atomic.IsAtomic<nint>());
        True(Atomic.IsAtomic<nuint>());
        True(Atomic.IsAtomic<object>());
        True(Atomic.IsAtomic<ValueTuple<object>>());

        False(Atomic.IsAtomic<ValueTuple<byte, long>>());
        False(Atomic.IsAtomic<ValueTuple<byte, byte>>());
        False(Atomic.IsAtomic<Guid>());
    }
}