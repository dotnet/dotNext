using System;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Identifies loop.
    /// </summary>
    /// <remarks>
    /// This type can be used to transfer control between outer and inner loops.
    /// </remarks>
    public readonly struct LoopCookie : IDisposable
    {
        private readonly WeakReference<LoopBuilderBase> scope;

        internal LoopCookie(LoopBuilderBase scope) => this.scope = new WeakReference<LoopBuilderBase>(scope);

        internal bool TryGetScope(out LoopBuilderBase scope) => this.scope.TryGetTarget(out scope);

        void IDisposable.Dispose() => scope.SetTarget(null);
    }
}
