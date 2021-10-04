using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents RPC response.
/// </summary>
/// <typeparam name="T">The type of RPC response.</typeparam>
[StructLayout(LayoutKind.Auto)]
public readonly struct Result<T>
{
    private readonly T result;

    /// <summary>
    /// Initializes a new result.
    /// </summary>
    /// <param name="term">The term provided by remote node.</param>
    /// <param name="value">The value returned by remote node.</param>
    public Result(long term, T value)
    {
        Term = term;
        result = value;
    }

    /// <summary>
    /// Gets term of the remote member.
    /// </summary>
    public long Term { get; }

    /// <summary>
    /// Gets RPC response.
    /// </summary>
    public T Value
    {
        get => result;
        init => result = value;
    }
}