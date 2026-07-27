namespace DotNext.Threading;

public sealed class AtomicTests : Test
{
    [Fact]
    public static void AtomicULongTest()
    {
        var i = 80UL;
        Equal(80UL, Atomic.Read(in i));
        
        Atomic.Write(ref i, 90UL);
        Equal(90UL, i);
    }

    [Fact]
    public static void AtomicLongTest()
    {
        var i = 80L;
        Equal(80L, Atomic.Read(in i));
        
        Atomic.Write(ref i, 90L);
        Equal(90L, i);
    }
    
    [Fact]
    public static void AtomicDoubleTest()
    {
        var i = 80D;
        Equal(80D, Atomic.Read(in i));
        
        Atomic.Write(ref i, 90D);
        Equal(90D, i);
    }

    [Fact]
    public static void AtomicBooleanTest()
    {
        var value = false;
        True(Interlocked.FalseToTrue(ref value));
        False(Interlocked.FalseToTrue(ref value));
        True(Interlocked.TrueToFalse(ref value));
        False(Interlocked.TrueToFalse(ref value));
        True(Interlocked.NegateAndGet(ref value));
        True(Interlocked.GetAndNegate(ref value));
        False(value);
        value = true;
        True(Interlocked.GetAndAccumulate(ref value, false, static (current, update) =>
        {
            True(current);
            False(update);
            return current & update;
        }));
        False(value);
        True(Interlocked.AccumulateAndGet(ref value, true, static (current, update) => current | update));
        True(value);

        value = false;
        True(Interlocked.UpdateAndGet(ref value, static x => !x));
        True(value);
    }

    [Fact]
    public static void IsAtomicWrite()
    {
        True(Interlocked.IsAtomic<byte>());
        True(Interlocked.IsAtomic<sbyte>());
        True(Interlocked.IsAtomic<bool>());
        True(Interlocked.IsAtomic<short>());
        True(Interlocked.IsAtomic<ushort>());
        True(Interlocked.IsAtomic<int>());
        True(Interlocked.IsAtomic<uint>());
        True(Interlocked.IsAtomic<long>());
        True(Interlocked.IsAtomic<ulong>());
        True(Interlocked.IsAtomic<nint>());
        True(Interlocked.IsAtomic<nuint>());
        True(Interlocked.IsAtomic<object>());
        True(Interlocked.IsAtomic<ValueTuple<object>>());

        False(Interlocked.IsAtomic<ValueTuple<byte, long>>());
        False(Interlocked.IsAtomic<Guid>());
    }
}