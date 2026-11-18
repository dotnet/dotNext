using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Threading;

[StructLayout(LayoutKind.Auto)]
internal struct SeqLock
{
    private nuint version; // even = stable, odd = write in progress

    public readonly bool IsWriteLockHeld
        => (Volatile.Read(in version) & 1U) is 1U;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public nuint EnterWriteLock()
    {
        var stamp = version;
        return TryEnterWriteLock(ref version, stamp) ? stamp : Contention(ref version);

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
    
    public bool TryEnterWriteLock(nuint stamp) => TryEnterWriteLock(ref version, stamp);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ExitWriteLock(nuint stamp) => Volatile.Write(ref version, stamp + 2U);
    
    private static bool TryEnterWriteLock(ref nuint version, nuint stamp)
        => (stamp & 1U) is 0U && Interlocked.CompareExchange(ref version, stamp + 1U, stamp) == stamp;

    public readonly bool TryOptimisticRead(out nuint stamp)
        => ((stamp = Volatile.Read(in version)) & 1U) is 0U;

    public readonly bool Validate(nuint stamp)
    {
        Volatile.ReadBarrier(); // reads of the protected data must complete before re-reading the version
        return stamp == version;
    }
}