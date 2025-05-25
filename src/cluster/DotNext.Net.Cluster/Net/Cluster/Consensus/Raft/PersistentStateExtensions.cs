namespace DotNext.Net.Cluster.Consensus.Raft;

using Buffers.Binary;
using IO.Log;
using Text.Json;

/// <summary>
/// Provides various extension methods for <see cref="IPersistentState"/> interface.
/// </summary>
public static class PersistentStateExtensions
{
    private sealed class TermReader : ILogEntryConsumer<IRaftLogEntry, long>
    {
        internal static readonly ILogEntryConsumer<IRaftLogEntry, long> Instance = new TermReader();

        private TermReader()
        {
        }

        ValueTask<long> ILogEntryConsumer<IRaftLogEntry, long>.ReadAsync<TEntryImpl, TList>(TList entries, long? snapshotIndex, CancellationToken token)
            => new(entries[0].Term);

        bool ILogEntryConsumer<IRaftLogEntry, long>.LogEntryMetadataOnly => true;
    }

    internal static ValueTask<long> GetTermAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, CancellationToken token)
        => index == 0L ? new(0L) : auditTrail.ReadAsync(TermReader.Instance, index, index, token);

    internal static async ValueTask<bool> IsUpToDateAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
    {
        var localIndex = auditTrail.LastEntryIndex;
        return index >= localIndex && term >= await auditTrail.GetTermAsync(localIndex, token).ConfigureAwait(false);
    }

    internal static async ValueTask<bool> ContainsAsync(this IAuditTrail<IRaftLogEntry> auditTrail, long index, long term, CancellationToken token)
        => index <= auditTrail.LastEntryIndex && term == await auditTrail.GetTermAsync(index, token).ConfigureAwait(false);

    internal static ValueTask<long> AppendNoOpEntry(this IPersistentState auditTrail, CancellationToken token)
        => auditTrail.AppendAsync(new EmptyLogEntry { Term = auditTrail.Term }, token);

    /// <summary>
    /// Appends a block of bytes to the log tail.
    /// </summary>
    /// <param name="state">The log.</param>
    /// <param name="payload">The log entry payload.</param>
    /// <param name="context">The optional context to be passed to the state machine.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The index of the added command within the log.</returns>
    public static ValueTask<long> AppendAsync(this IPersistentState state, ReadOnlyMemory<byte> payload, object? context = null,
        CancellationToken token = default)
        => state.AppendAsync<BinaryLogEntry>(new() { Content = payload, Term = state.Term, Context = context }, token);

    /// <summary>
    /// Appends a binary object to the log tail.
    /// </summary>
    /// <param name="state">The log.</param>
    /// <param name="payload">The log entry payload.</param>
    /// <param name="context">The optional context to be passed to the state machine.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the binary object.</typeparam>
    /// <returns>The index of the added command within the log.</returns>
    public static ValueTask<long> AppendAsync<T>(this IPersistentState state, T payload, object? context = null,
        CancellationToken token = default)
        where T : IBinaryFormattable<T>
        => state.AppendAsync<BinaryLogEntry<T>>(new() { Content = payload, Term = state.Term, Context = context }, token);

    /// <summary>
    /// Appends JSON objec to the log tail.
    /// </summary>
    /// <param name="state">The log.</param>
    /// <param name="payload">The log entry payload.</param>
    /// <param name="context">The optional context to be passed to the state machine.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <typeparam name="T">The type of the binary object.</typeparam>
    /// <returns>The index of the added command within the log.</returns>
    public static ValueTask<long> AppendJsonAsync<T>(this IPersistentState state, T payload, object? context = null,
        CancellationToken token = default)
        where T : IJsonSerializable<T>
        => state.AppendAsync<JsonLogEntry<T>>(new() { Content = payload, Term = state.Term, Context = context }, token);
}