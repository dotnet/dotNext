using System;
using System.Linq.Expressions;
using System.Runtime.InteropServices;

namespace DotNext.Metaprogramming
{
    using Linq.Expressions;

    /// <summary>
    /// Identifies loop.
    /// </summary>
    /// <remarks>
    /// This type can be used to transfer control between outer and inner loops.
    /// The context lifetime is limited by surrounding lexical scope of the loop.
    /// </remarks>
    public struct LoopContext : IDisposable
    {
        private readonly GCHandle loop;

        internal LoopContext(ILoopLabels loop) => this.loop = GCHandle.Alloc(loop, GCHandleType.Weak);

        private ILoopLabels GetLabels() => loop.Target is ILoopLabels result ? result : throw new ObjectDisposedException(nameof(LoopContext));

        internal LabelTarget ContinueLabel => GetLabels().ContinueLabel;

        internal LabelTarget BreakLabel => GetLabels().BreakLabel;

        void IDisposable.Dispose()
        {
            loop.Free();
            this = default;
        }
    }
}
