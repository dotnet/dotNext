using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Threading;

[DebuggerTypeProxy(typeof(DebugView))]
partial class Epoch
{
    [ExcludeFromCodeCoverage]
    private string GetDebugView(uint epoch) => entries[epoch].DebugView;
    
    [ExcludeFromCodeCoverage]
    private struct DebugView(Epoch epoch)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public ReadOnlySpan<Entry> Epochs => epoch.entries;
    }
}