using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft;

/// <summary>
/// Represents RPC response.
/// </summary>
/// <typeparam name="T">The type of RPC response.</typeparam>
/// <param name="Term">The term of the remote member.</param>
/// <param name="Value">Raft RPC response.</param>
[StructLayout(LayoutKind.Auto)]
public readonly record struct Result<T>(long Term, T Value);