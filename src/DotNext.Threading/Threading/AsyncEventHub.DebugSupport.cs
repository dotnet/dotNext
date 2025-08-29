using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using DotNext.Numerics;

namespace DotNext.Threading;

[DebuggerTypeProxy(typeof(DebugView))]
public partial class AsyncEventHub
{
    [ExcludeFromCodeCoverage]
    private readonly struct DebugView
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public readonly bool[] States;

        public DebugView(AsyncEventHub hub)
        {
            var state = hub.CaptureState();
            Span<bool> flags = stackalloc bool[MaxCount];
            state.Mask.GetBits(flags);
            States = flags[..hub.Count].ToArray();
        }
    }

    /// <summary>
    /// Captures the state of the events.
    /// </summary>
    /// <returns>A group of signaled events.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public EventGroup CaptureState()
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        EventGroup result;
        lock (SyncRoot)
        {
            result = new(state.Value);
        }

        return result;
    }
}