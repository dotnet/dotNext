using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[DebuggerTypeProxy(typeof(DebugView))]
public partial class Epoch
{
    [ExcludeFromCodeCoverage]
    private struct DebugView(Epoch epoch)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ReadOnlySpan<Entry> Epochs => epoch.state.Entries;
    }
}