using System.Diagnostics;

namespace DotNext.Threading.Tasks;

using Lock = System.Threading.Lock;

public partial class ManualResetCompletionSource
{
    private readonly Lock SyncRoot = new();

    private protected Lock.Scope AcquireLock() => SyncRoot.EnterScope();

    [Conditional("DEBUG")]
    private protected void AssertLocked() => Debug.Assert(SyncRoot.IsHeldByCurrentThread);
}