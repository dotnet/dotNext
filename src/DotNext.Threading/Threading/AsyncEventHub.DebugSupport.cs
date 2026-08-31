using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using DotNext.Numerics;

namespace DotNext.Threading;

//[DebuggerTypeProxy(typeof(DebugView))]
public partial class AsyncEventHub
{
    [ExcludeFromCodeCoverage]
    private readonly struct DebugView(AsyncEventHub hub)
    {
        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public readonly State CurrentState = hub.CaptureState().Mask;
    }

    /// <summary>
    /// Captures the state of the events.
    /// </summary>
    /// <returns>A group of signaled events.</returns>
    [EditorBrowsable(EditorBrowsableState.Advanced)]
    public EventGroup CaptureState()
    {
        ObjectDisposedException.ThrowIf(IsDisposingOrDisposed, this);

        Unsafe.SkipInit(out State captured);
        TryAcquire(new CapturedState(in state, ref captured), out _).Dispose();
        return new(captured);
    }

    [StructLayout(LayoutKind.Auto)]
    private readonly ref struct CapturedState(ref readonly State current, ref State captured) : ILockManager
    {
        private readonly ref readonly State current = ref current;
        private readonly ref State captured = ref captured;

        bool ILockManager.IsLockAllowed => true;

        void ILockManager.AcquireLock() => captured = current.Clone();

        static bool ILockManager.RequiresEmptyQueue => false;
    }
}