using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace DotNext.Threading.Tasks;

public partial class ManualResetCompletionSource
{
    private protected object SyncRoot => cancellationCallback;

    [Conditional("DEBUG")]
    private protected void AssertLocked() => Debug.Assert(Monitor.IsEntered(SyncRoot));
}