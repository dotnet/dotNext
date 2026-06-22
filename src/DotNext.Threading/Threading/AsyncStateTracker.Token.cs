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
        internal readonly ulong Version;

        internal Token(ulong version) => Version = version;

        internal Token Next()
        {
            var version = Version;
            ChangeVersion(ref version);
            return new(version);
        }

        /// <inheritdoc/>
        public override string ToString() => Version.ToString();
    }
}