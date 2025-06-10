using System.IO.Pipelines;
using System.Net.Sockets;

namespace DotNext.Net.Multiplexing;

/// <summary>
/// Represents multiplexed client.
/// </summary>
public abstract partial class MultiplexedClient : Disposable, IAsyncDisposable
{
    private volatile CancellationTokenSource? lifetimeTokenSource;

    protected MultiplexedClient(int fragmentSize = 1380, PipeOptions? options = null)
    {
        if ((uint)fragmentSize is 0U or >= ushort.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(fragmentSize));

        streams = new();
        sendBuffer = GC.AllocateArray<byte>(fragmentSize, pinned: true);
        receiveBuffer = GC.AllocateArray<byte>(fragmentSize, pinned: true);
        writeSignal = new(initialState: false);
        this.options = options ?? PipeOptions.Default;
        dispatcher = DispatchAsync((lifetimeTokenSource = new()).Token);
    }

    /// <summary>
    /// Creates a new multiplexed client stream.
    /// </summary>
    /// <returns></returns>
    public IDuplexPipe CreateStream()
    {
        var stream = new StreamHandler(options, writeSignal);
        ulong id;
        do
        {
            id = Interlocked.Increment(ref streamId);
        } while (!streams.TryAdd(id, stream));

        writeSignal.Set();
        return stream;
    }

    /// <summary>
    /// Creates a connected socket.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The connected socket.</returns>
    protected abstract ValueTask<Socket> ConnectAsync(CancellationToken token);

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cancel();
        }
        
        base.Dispose(disposing);
    }

    private void Cancel()
    {
        if (Interlocked.Exchange(ref lifetimeTokenSource, null) is { } cts)
        {
            using (cts)
            {
                cts.Cancel();
            }
        }
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        Cancel();
        await dispatcher.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}