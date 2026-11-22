using System.Runtime.InteropServices;

namespace DotNext.Threading;

public partial class AsyncReaderWriterLock
{
    /// <summary>
    /// Represents lock stamp used for optimistic reading.
    /// </summary>
    [StructLayout(LayoutKind.Auto)]
    public readonly record struct LockStamp
    {
        internal readonly ulong Version;

        internal LockStamp(ulong version) => Version = version;

        /// <inheritdoc/>
        public override string ToString() => Version.ToString();
    }
}