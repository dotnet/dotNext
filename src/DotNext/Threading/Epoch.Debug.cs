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
        public IEnumerable<string> Epochs
        {
            get
            {
                foreach (var entry in epoch.epochs)
                {
                    yield return entry.DebugView;
                }
            }
        }
    }
}