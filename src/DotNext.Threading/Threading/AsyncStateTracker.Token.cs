using System.Runtime.InteropServices;

namespace DotNext.Threading;

partial class AsyncStateTracker
{
    /// <summary>
    /// Represents a version of the state.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct Token
    {
        internal readonly nuint Version;

        internal Token(nuint version) => Version = version;

        internal Token Next() => new(Version + 1U);

        /// <inheritdoc/>
        public override string ToString() => Version.ToString();
    }
}