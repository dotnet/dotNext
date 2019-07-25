using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    internal struct SpinLockSlim
    {
        private AtomicBoolean state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Acquire()
        {
            for (SpinWait spinner; state.CompareExchange(true, false); spinner.SpinOnce()) { }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Release() => state.Value = false;
    }
}
