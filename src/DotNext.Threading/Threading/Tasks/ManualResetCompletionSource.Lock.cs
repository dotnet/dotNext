using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks;

public partial class ManualResetCompletionSource
{
    private int lockState;

    // a chance of lock contention for this instance is very low
    // so monitor lock is too heavyweight for synchronization purposes
    private protected void EnterLock()
    {
        ref var lockState = ref this.lockState;
        if (Interlocked.Exchange(ref lockState, 1) is 1)
            Contention(ref lockState);

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void Contention(ref int lockState)
        {
            var spinner = new SpinWait();
            do
            {
                spinner.SpinOnce();
            }
            while (Interlocked.Exchange(ref lockState, 1) is 1);
        }
    }

    private bool TryEnterLock() => Interlocked.Exchange(ref lockState, 1) is 0;

    private protected void ExitLock()
    {
        AssertLocked();

        Volatile.Write(ref lockState, 0);
    }

    [Conditional("DEBUG")]
    private protected void AssertLocked() => Debug.Assert(lockState is not 0);
}