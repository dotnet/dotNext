using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents RPC response.
/// </summary>
/// <typeparam name="T">The type of RPC response.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Result<T>
{
    /// <summary>
    /// Gets term of the remote member.
    /// </summary>
    required public long Term { get; init; }

    /// <summary>
    /// Gets RPC response.
    /// </summary>
    public T Value { get; init; }

    internal Result<TOther> SetValue<TOther>(TOther value) => new() { Term = Term, Value = value };
}