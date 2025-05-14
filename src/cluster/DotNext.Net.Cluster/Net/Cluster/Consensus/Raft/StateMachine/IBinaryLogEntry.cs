namespace DotNext.Net.Cluster.Consensus.Raft.StateMachine;

using Buffers;
using IO;

/// <summary>
/// Represents a log entry that can be directly written
/// </summary>
public interface IBinaryLogEntry : IRaftLogEntry
{
    /// <summary>
    /// Writes the log entry payload to the specified buffer.
    /// </summary>
    /// <param name="buffer">The buffer that is at least of <see cref="Length"/> size.</param>
    void WriteTo(Span<byte> buffer);
    
    /// <summary>
    /// Gets the length of this log entry, in bytes.
    /// </summary>
    new int Length { get; }

    /// <inheritdoc/>
    ValueTask IDataTransferObject.WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
    {
        var length = Length;
        
        if (length is 0)
            return ValueTask.CompletedTask;
        
        if (TryGetMemory(out var memory))
            return writer.Invoke(memory, token);

        var buffer = writer.Buffer;
        if (buffer.Length >= length)
        {
            WriteTo(buffer.Span.Slice(0, length));
            return writer.AdvanceAsync(length, token);
        }

        return WriteToAsync(writer, token);
    }

    private new async ValueTask WriteToAsync<TWriter>(TWriter writer, CancellationToken token)
        where TWriter : IAsyncBinaryWriter
    {
        var buffer = Memory.AllocateExactly<byte>(Length);
        WriteTo(buffer.Span);
        try
        {
            await writer.Invoke(buffer.Memory, token).ConfigureAwait(false);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <inheritdoc/>
    long? IDataTransferObject.Length => Length;
}