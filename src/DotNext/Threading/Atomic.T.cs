using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

using Runtime.CompilerServices;

partial struct Atomic<T>
{
    private T value;
    private nuint version; // even = stable, odd = write in progress (seqlock)
    
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
        => TryRead<T, CopyOperation>(out var result, out var stamp)
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
    private nuint EnterWriteLock()
    {
        var stamp = version;
        return TryEnterWriteLock(stamp) ? stamp : Contention(ref version);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static nuint Contention(ref nuint version)
        {
            for (var spinner = new SpinWait(); ; spinner.SpinOnce())
            {
                var stamp = version;
                if (TryEnterWriteLock(ref version, stamp))
                    return stamp;
            }
        }
    }

    private static bool TryEnterWriteLock(ref nuint version, nuint stamp)
        => (stamp & 1U) is 0U && Interlocked.CompareExchange(ref version, stamp + 1U, stamp) == stamp;

    private bool TryEnterWriteLock(nuint stamp) => TryEnterWriteLock(ref version, stamp);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ExitWriteLock(nuint stamp) => Volatile.Write(ref version, stamp + 2U);

    internal bool TryWrite(nuint stamp, in T newValue)
    {
        var entered = TryEnterWriteLock(stamp);
        if (entered)
        {
            RuntimeHelpers.Copy(in newValue, out value);
            ExitWriteLock(stamp);
        }

        return entered;
    }

    internal readonly nuint Read(ref SpinWait spinner, out T result)
        => Read<T, CopyOperation>(ref spinner, out result);

    internal readonly nuint Read<TResult, TOperation>(ref SpinWait spinner, out TResult result)
        where TOperation : struct, IReadOperation<TResult>, allows ref struct
    {
        nuint stamp;
        while (!TryRead<TResult, TOperation>(out result, out stamp))
        {
            spinner.SpinOnce();
        }

        return stamp;
    }

    private readonly bool TryRead<TResult, TOperation>(out TResult result, out nuint stamp)
        where TOperation : struct, IReadOperation<TResult>, allows ref struct
    {
        Unsafe.SkipInit(out result);
        Unsafe.SkipInit(out stamp);

        var currentStamp = Volatile.Read(in version);
        if ((currentStamp & 1U) is 1U)
            return false;

        TOperation.Invoke(in value, out result);
        Volatile.ReadBarrier();
        stamp = currentStamp;
        return currentStamp == version;
    }
    
    internal interface IReadOperation<TResult>
    {
        public static abstract void Invoke(in T input, out TResult output);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CopyOperation : IReadOperation<T>
    {
        static void IReadOperation<T>.Invoke(in T value, out T destination) => RuntimeHelpers.Copy(in value, out destination);
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