using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents RPC response.
/// </summary>
/// <typeparam name="T">The type of RPC response.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Result<T> // TODO: Change to required init properties in C# 12
{
    /// <summary>
    /// Initializes a new result.
    /// </summary>
    /// <param name="term">The term provided by remote node.</param>
    /// <param name="value">The value returned by remote node.</param>
    public Result(long term, T value)
    {
        Term = term;
        Value = value;
    }

    /// <summary>
    /// Gets term of the remote member.
    /// </summary>
    public long Term { get; internal init; }

    /// <summary>
    /// Gets RPC response.
    /// </summary>
    public T Value { get; internal init; }
}