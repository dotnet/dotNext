using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.CompilerServices;

partial struct Atomic<T>
{
    private T value;
    private uint version; // even = stable, odd = write in progress (seqlock)
    
    private bool CompareAndSet<TComparer>(TComparer comparer, in T expected, in T update)
        where TComparer : struct, IEqualityComparer, allows ref struct
    {
        for (var spinner = new SpinWait();; spinner.SpinOnce())
        {
            var stamp = Read(ref spinner, out var current);
            if (!comparer.Equals(in current, in expected))
                return false;

            if (TryWrite(stamp, in update))
                return true;
        }
    }
    
    private bool TryUpdate<TComparer>(TComparer comparer, in T comparisonValue, in T newValue)
        where TComparer : struct, IEqualityComparer, allows ref struct
        => TryRead(out var result, out var stamp)
           && comparer.Equals(in result, in comparisonValue)
           && TryWrite(stamp, in newValue);
    
    private bool CompareExchange<TComparer>(TComparer comparer, in T update, in T expected, out T result)
        where TComparer : struct, IEqualityComparer, allows ref struct
    {
        for (var spinner = new SpinWait();; spinner.SpinOnce())
        {
            var stamp = Read(ref spinner, out var current);
            RuntimeHelpers.Copy(in current, out result);
            if (!comparer.Equals(in current, in expected))
                return false;

            if (TryWrite(stamp, in update))
                return true;
        }
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint EnterWriteLock()
    {
        var stamp = version;
        return TryEnterWriteLock(stamp) ? stamp : Contention(ref version);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static uint Contention(ref uint version)
        {
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                var stamp = version;
                if (TryEnterWriteLock(ref version, stamp))
                    return stamp;
            }
        }
    }

    private static bool TryEnterWriteLock(ref uint version, uint stamp)
        => (stamp & 1U) is 0U && Interlocked.CompareExchange(ref version, stamp + 1U, stamp) == stamp;

    private bool TryEnterWriteLock(uint stamp) => TryEnterWriteLock(ref version, stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitWriteLock(uint stamp) => Volatile.Write(ref version, stamp + 2U);

    private bool TryWrite(uint stamp, in T newValue)
    {
        var entered = TryEnterWriteLock(stamp);
        if (entered)
        {
            RuntimeHelpers.Copy(in newValue, out value);
            ExitWriteLock(stamp);
        }

        return entered;
    }
    
    private readonly uint Read(ref SpinWait spinner, out T result)
    {
        uint stamp;
        while (!TryRead(out result, out stamp))
        {
            spinner.SpinOnce();
        }

        return stamp;
    }

    private readonly bool TryRead(out T result, out uint stamp)
    {
        Unsafe.SkipInit(out result);
        Unsafe.SkipInit(out stamp);

        var currentStamp = Volatile.Read(in version);
        if ((currentStamp & 1U) is not 0U)
            return false;

        RuntimeHelpers.Copy(in value, out result);
        Volatile.ReadBarrier();
        stamp = currentStamp;
        return currentStamp == version;
    }
    
    private interface IEqualityComparer
    {
        bool Equals(in T x, in T y);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DefaultEqualityComparer : IEqualityComparer
    {
        bool IEqualityComparer.Equals(in T x, in T y) => EqualityComparer<T>.Default.Equals(x, y);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly struct DelegatingEqualityComparer(Func<T, T, bool> func) : IEqualityComparer
    {
        bool IEqualityComparer.Equals(in T x, in T y) => func(x, y);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly unsafe struct EqualityComparer(delegate*<in T, in T, bool> ptr) : IEqualityComparer
    {
        bool IEqualityComparer.Equals(in T x, in T y) => ptr(in x, in y);
    }
}