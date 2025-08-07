using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using Microsoft.AspNetCore.Connections;

namespace DotNext.Net.Multiplexing;

/// <summary>
/// Represents multiplexed client.
/// </summary>
public abstract partial class MultiplexedClient : Disposable, IAsyncDisposable
{
    private readonly TaskCompletionSource readiness;
    private Task dispatcher;
    
    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? lifetimeTokenSource;

    /// <summary>
    /// Initializes a new multiplexed client.
    /// </summary>
    /// <param name="options">The configuration of the client.</param>
    protected MultiplexedClient(Options options)
    {
        this.options = options.BufferOptions;
        writeSignal = new(initialState: false);
        var lifetimeToken = (lifetimeTokenSource = new()).Token;

        dispatcher = Task.CompletedTask;
        readiness = new(TaskCreationOptions.RunContinuationsAsynchronously);
        
        input = new(new(),
            writeSignal,
            GC.AllocateArray<byte>(options.FragmentSize, pinned: true),
            streamCount,
            options.MeasurementTags,
            options.Timeout,
            options.HeartbeatTimeout,
            lifetimeToken);
        output = input.CreateOutput(GC.AllocateArray<byte>(options.FragmentSize, pinned: true), options.Timeout);
    }

    /// <summary>
    /// Connects to the server and starts the dispatching loop.
    /// </summary>
    /// <returns>The task that resumes when the client socket is connected successfully to the server.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The client is disposed.</exception>
    public ValueTask StartAsync(CancellationToken token = default)
    {
        if (ReferenceEquals(dispatcher, Task.CompletedTask))
        {
            dispatcher = DispatchAsync();
        }

        return new(readiness.Task.WaitAsync(token));
    }

    /// <summary>
    /// Creates a new multiplexed client stream.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <remarks>
    /// If <see cref="IDuplexPipe.Output"/> is completed successfully, then the implementation
    /// doesn't expect the input data and remote <see cref="IDuplexPipe.Input"/> will be completed successfully as well.
    /// If <see cref="IDuplexPipe.Output"/> is completed with exception, then the implementation
    /// doesn't expect the input data, but remote <see cref="IDuplexPipe.Input"/> will not be completed.
    /// If <see cref="IDuplexPipe.Input"/> is completed (successfully or not), then the implementation
    /// skips any incoming packets for the associated stream.
    ///
    /// To deactivate the stream, the consumer needs to complete both <see cref="IDuplexPipe.Input"/> and <see cref="IDuplexPipe.Output"/>.
    ///
    /// <see cref="IDuplexPipe.Input"/> can be completed successfully if the remote <see cref="IDuplexPipe.Output"/> completes successfully.
    /// <see cref="IDuplexPipe.Input"/> can be completed with <see cref="StreamRejectedException"/> if the backlog of the remote peer is full,
    /// and the peer cannot accept the incoming stream. <see cref="TimeoutException"/> if the consumer of <see cref="IDuplexPipe.Input"/> is not
    /// fast enough to consume the incoming traffic. <see cref="ConnectionResetException"/> if the client or server is disposed.
    /// </remarks>
    /// <returns>A duplex pipe for data input/output.</returns>
    public ValueTask<IDuplexPipe> OpenStreamAsync(CancellationToken token = default)
        => readiness.Task.IsCompletedSuccessfully ? new(OpenStream()) : OpenStreamCoreAsync(token);

    private async ValueTask<IDuplexPipe> OpenStreamCoreAsync(CancellationToken token)
    {
        await readiness.Task.WaitAsync(token).ConfigureAwait(false);
        return OpenStream();
    }

    private IDuplexPipe OpenStream()
    {
        var stream = new StreamHandler(options, writeSignal);
        ulong id;
        do
        {
            id = Interlocked.Increment(ref streamId);
        } while (!input.TryAddStream(id, stream));
        
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
            dispatcher.ConfigureAwait(false).GetAwaiter().UnsafeOnCompleted(new Action(writeSignal.Dispose) + input.Dispose + output.Dispose);
        }

        base.Dispose(disposing);
    }

    private void Cancel()
    {
        if (Interlocked.Exchange(ref lifetimeTokenSource, null) is { } cts)
        {
            readiness.TrySetException(new ObjectDisposedException(GetType().Name));
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
        await input.DisposeAsync().ConfigureAwait(false);
        await output.DisposeAsync().ConfigureAwait(false);
        writeSignal.Dispose();
    }

    /// <inheritdoc cref="IAsyncDisposable.DisposeAsync"/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();
}