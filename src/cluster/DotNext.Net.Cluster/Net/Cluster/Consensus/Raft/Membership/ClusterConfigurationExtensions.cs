using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace DotNext.Net.Cluster.Consensus.Raft.Membership;

using Buffers;
using IO;
using IO.Log;

/// <summary>
/// Represents configuration extensions.
/// </summary>
public static class ClusterConfigurationExtensions
{
    /// <summary>
    /// Appends a new configuration as a log entry to the Write-Ahead Log.
    /// </summary>
    /// <param name="state">The persistent state.</param>
    /// <param name="configuration">The configuration to append.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="TAddress">The type of the address.</typeparam>
    /// <returns>The index of the added entry.</returns>
    public static ValueTask<long> AppendAsync<TAddress>(this IPersistentState state, IClusterConfiguration<TAddress> configuration,
        CancellationToken token = default)
        where TAddress : notnull
        => state.AppendAsync(configuration, state.Term, token);

    internal static ValueTask<long> AppendAsync<TAddress>(this IAuditTrail<IRaftLogEntry> state, IClusterConfiguration<TAddress> configuration,
        long term, CancellationToken token = default)
        where TAddress : notnull
        => configuration is ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>
            ? state.AppendAsync(new OptimizedClusterConfigurationLogEntry<TAddress>(configuration) { Term = term }, token)
            : state.AppendAsync(new ClusterConfigurationLogEntry<TAddress>(configuration) { Term = term }, token);
}

[StructLayout(LayoutKind.Auto)]
file readonly struct ClusterConfigurationLogEntry<TAddress>(IClusterConfiguration<TAddress> configuration) : IRaftLogEntry
    where TAddress : notnull
{
    public IClusterConfiguration<TAddress> Configuration => configuration;
    
    public bool IsReusable => configuration.IsReusable;

    public long? Length => configuration.Length;

    bool IRaftLogEntry.IsConfiguration => true;

    public ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
        => configuration.WriteToAsync(writer, token);

    public required long Term { get; init; }

    public bool TryGetMemory(out ReadOnlyMemory<byte> memory)
        => configuration.TryGetMemory(out memory);

    public ValueTask<TResult> TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        where TTransformation : IDataTransferObject.ITransformation<TResult>
        => configuration.TransformAsync<TResult, TTransformation>(transformation, token);
}

[StructLayout(LayoutKind.Auto)]
file readonly struct OptimizedClusterConfigurationLogEntry<TAddress> : IRaftLogEntry, ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>
    where TAddress : notnull
{
    private readonly ClusterConfigurationLogEntry<TAddress> entry;

    public OptimizedClusterConfigurationLogEntry(IClusterConfiguration<TAddress> configuration)
    {
        Debug.Assert(configuration is ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>);

        entry = new(configuration) { Term = 0L };
    }

    bool IDataTransferObject.IsReusable => entry.IsReusable;

    long? IDataTransferObject.Length => entry.Length;

    bool IRaftLogEntry.IsConfiguration => true;

    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        => entry.WriteToAsync(writer, token);

    public required long Term
    {
        get => entry.Term;
        init => entry = entry with { Term = value };
    }

    bool IDataTransferObject.TryGetMemory(out ReadOnlyMemory<byte> memory)
        => entry.TryGetMemory(out memory);

    ValueTask<TResult> IDataTransferObject.TransformAsync<TResult, TTransformation>(TTransformation transformation, CancellationToken token)
        => entry.TransformAsync<TResult, TTransformation>(transformation, token);

    MemoryOwner<byte> ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>.Invoke(MemoryAllocator<byte> allocator)
        => Unsafe.As<ISupplier<MemoryAllocator<byte>, MemoryOwner<byte>>>(entry.Configuration).Invoke(allocator);
}