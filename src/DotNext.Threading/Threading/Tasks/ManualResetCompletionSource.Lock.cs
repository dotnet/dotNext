using System.Diagnostics;

namespace DotNext.Threading.Tasks;

using Lock = System.Threading.Lock;

public partial class ManualResetCompletionSource
{
    private readonly Lock syncRoot = new();

    private protected Lock.Scope AcquireLock() => syncRoot.EnterScope();

    [Conditional("DEBUG")]
    private protected void AssertLocked() => Debug.Assert(syncRoot.IsHeldByCurrentThread);
}