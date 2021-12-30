using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
            hub.CaptureState(States = new bool[hub.Count]);
        }
    }

    /// <summary>
    /// Captures the state of the events.
    /// </summary>
    /// <remarks>
    /// Each element of the buffer will be modified with the state of the event matching
    /// to the index of the element.
    /// </remarks>
    /// <param name="states">A buffer of states to fill.</param>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public void CaptureState(Span<bool> states)
    {
        lock (accessLock)
        {
            for (var i = 0; i < Math.Min(states.Length, Count); i++)
                Unsafe.Add(ref MemoryMarshal.GetReference(states), i) = Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(sources), i).Task.IsCompleted;
        }
    }
}