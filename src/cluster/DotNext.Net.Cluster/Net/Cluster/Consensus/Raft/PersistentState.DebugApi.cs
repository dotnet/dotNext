using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft;

public partial class PersistentState
{
    /// <summary>
    /// Changes the index of the last added entry.
    /// </summary>
    /// <remarks>
    /// This method should not be used in production.
    /// </remarks>
    /// <param name="index">The index.</param>
    [Conditional("DEBUG")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [CLSCompliant(false)]
    [ExcludeFromCodeCoverage]
    public void DbgChangeLastIndex(long index) => LastEntryIndex = index;
}