using System;

namespace DotNext.Metaprogramming
{
    /// <summary>
    /// Identifies loop.
    /// </summary>
    /// <remarks>
    /// This type can be used to transfer control between outer and inner loops.
    /// </remarks>
    public readonly struct LoopContext : IDisposable
    {
        private readonly WeakReference<LoopScopeBase> scope;

        internal LoopContext(LoopScopeBase scope) => this.scope = new WeakReference<LoopScopeBase>(scope);

        internal bool TryGetScope(out LoopScopeBase scope) => this.scope.TryGetTarget(out scope);

        void IDisposable.Dispose() => scope.SetTarget(null);
    }
}
