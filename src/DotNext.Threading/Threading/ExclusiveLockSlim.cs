using System.Runtime.CompilerServices;
using System.Threading;

namespace DotNext.Threading
{
    internal struct ExclusiveLockSlim
    {
        private AtomicBoolean state;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Acquire()
        {
            for (SpinWait spinner; !state.FalseToTrue(); spinner.SpinOnce()) { }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Release() => state.Value = false;
    }
}
