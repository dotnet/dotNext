using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.TransportServices.ConnectionOriented;

using Buffers;

/// <summary>
/// Provides encoding/decoding routines for transmitting Raft-specific
/// RPC calls over stream-oriented network transports.
/// </summary>
internal abstract partial class ProtocolStream : Stream, IResettable
{
    private const int FrameHeadersSize = sizeof(int);

    private readonly MemoryAllocator<byte> allocator;
    private MemoryOwner<byte> buffer;

    // for reader, both fields are in use
    // for writer, bufferStart is a beginning of the frame
    private int bufferStart, bufferEnd;

    private protected ProtocolStream(MemoryAllocator<byte> allocator, int transmissionBlockSize)
    {
        Debug.Assert(transmissionBlockSize > 0);

        buffer = allocator.Invoke(transmissionBlockSize, exactSize: false);
        this.allocator = allocator;
    }

    private protected abstract ValueTask WriteToTransportAsync(ReadOnlyMemory<byte> buffer, CancellationToken token);

    private protected virtual void WriteToTransport(ReadOnlySpan<byte> buffer)
    {
        var localBuffer = buffer.Copy(allocator);
        var timeoutTracker = new CancellationTokenSource(WriteTimeout);
        var task = WriteToTransportAsync(localBuffer.Memory, timeoutTracker.Token).AsTask();
        try
        {
            task.Wait();
        }
        catch (OperationCanceledException e)
        {
            throw new TimeoutException(e.Message, e);
        }
        finally
        {
            localBuffer.Dispose();
            timeoutTracker.Dispose();
            task.Dispose();
        }
    }

    private protected abstract ValueTask<int> ReadFromTransportAsync(Memory<byte> buffer, CancellationToken token);

    private protected virtual int ReadFromTransport(Span<byte> buffer)
    {
        int result;
        var localBuffer = allocator.Invoke(buffer.Length, exactSize: true);
        var timeoutTracker = new CancellationTokenSource(ReadTimeout);
        var task = ReadFromTransportAsync(localBuffer.Memory, timeoutTracker.Token).AsTask();
        try
        {
            task.Wait();
            result = task.Result;
            localBuffer.Span.CopyTo(buffer);
        }
        catch (OperationCanceledException e)
        {
            throw new TimeoutException(e.Message, e);
        }
        finally
        {
            localBuffer.Dispose();
            timeoutTracker.Dispose();
            task.Dispose();
        }

        return result;
    }

    private protected abstract ValueTask<int> ReadFromTransportAsync(int minimumSize, Memory<byte> buffer, CancellationToken token);

    private protected virtual int ReadFromTransport(int minimumSize, Span<byte> buffer)
    {
        int result;
        var localBuffer = allocator.Invoke(buffer.Length, exactSize: true);
        var timeoutTracker = new CancellationTokenSource(ReadTimeout);
        var task = ReadFromTransportAsync(minimumSize, localBuffer.Memory, timeoutTracker.Token).AsTask();
        try
        {
            task.Wait();
            result = task.Result;
            localBuffer.Span.CopyTo(buffer);
        }
        catch (OperationCanceledException e)
        {
            throw new TimeoutException(e.Message, e);
        }
        finally
        {
            localBuffer.Dispose();
            timeoutTracker.Dispose();
            task.Dispose();
        }

        return result;
    }

    internal Memory<byte> RemainingBuffer => buffer.Memory.Slice(bufferEnd);

    internal Span<byte> RemainingBufferSpan => buffer.Span.Slice(bufferEnd);

    internal ReadOnlySpan<byte> WrittenBufferSpan => buffer.Span[bufferStart..bufferEnd];

    public void Reset()
    {
        bufferStart = bufferEnd = 0;
        ResetReadState();
    }

    internal void ResetReadState()
    {
        readState = ReadState.FrameNotStarted;
        frameSize = 0;
    }

    protected override void Dispose(bool disposing)
    {
        buffer.Dispose();
        base.Dispose(disposing);
    }
}