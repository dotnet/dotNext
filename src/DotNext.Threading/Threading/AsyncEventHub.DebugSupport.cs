using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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

        Unsafe.SkipInit(out UInt128 captured);
        TryAcquire(new CapturedState(in state, ref captured), out _).Dispose();
        return new(captured);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CapturedState(ref readonly UInt128 current, ref UInt128 captured) : ILockManager
    {
        private readonly ref readonly UInt128 current = ref current;
        private readonly ref UInt128 captured = ref captured;

        bool ILockManager.IsLockAllowed => true;

        void ILockManager.AcquireLock() => captured = current;

        static bool ILockManager.RequiresEmptyQueue => false;
    }
}