using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading.Channels;

namespace DotNext.Net.Multiplexing;

using Buffers;
using Threading;

/// <summary>
/// Represents multiplexed listener.
/// </summary>
[Experimental("DOTNEXT001")]
public abstract partial class MultiplexedListener : Disposable, IAsyncDisposable
{
    private readonly CancellationToken lifetimeToken;
    private readonly TimeSpan heartbeatTimeout, timeout;
    private readonly Channel<MultiplexedStream> backlog;
    private readonly MemoryAllocator<byte> allocator;
    private readonly int flushThreshold;
    private readonly TaskCompletionSource readiness;
    private readonly MultiplexedStreamFactory streamFactory;
    private Task listener;

    [SuppressMessage("Usage", "CA2213", Justification = "False positive")]
    private volatile CancellationTokenSource? lifetimeTokenSource;

    /// <summary>
    /// Initializes a new multiplexed listener.
    /// </summary>
    /// <param name="configuration"></param>
    protected MultiplexedListener(Options configuration)
    {
        backlog = Channel.CreateBounded<MultiplexedStream>(new BoundedChannelOptions(configuration.Backlog)
        {
            AllowSynchronousContinuations = false,
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleWriter = false,
            SingleReader = false,
        });

        allocator = configuration.ToAllocator();
        streamFactory = new MultiplexedStreamFactoryImpl(configuration.BufferOptions, backlog.Writer);
        measurementTags = configuration.MeasurementTags;
        flushThreshold = configuration.BufferCapacity;
        readiness = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        lifetimeToken = (lifetimeTokenSource = new()).Token;
        heartbeatTimeout = configuration.HeartbeatTimeout;
        timeout = configuration.Timeout;
        listener = Task.CompletedTask;
    }

    /// <summary>
    /// Accepts the incoming stream asynchronously.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The incoming stream.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The listener is disposed.</exception>
    /// <seealso cref="MultiplexedClient.OpenStreamAsync"/>
    /// <seealso cref="DotNext.IO.Pipelines.DuplexStream"/>
    public async ValueTask<IDuplexPipe> AcceptAsync(CancellationToken token = default)
    {
        MultiplexedStream result;
        try
        {
            while (true)
            {
                result = await backlog.Reader.ReadAsync(token).ConfigureAwait(false);

                if (!result.IsTransportSideCompleted)
                    break;

                await result.AbortAppSideAsync().ConfigureAwait(false);
            }
        }
        catch (ChannelClosedException)
        {
            throw new ObjectDisposedException(GetType().Name);
        }

        PendingStreamCount.Add(-1L);
        return result;
    }

    /// <summary>
    /// Ensures that the listener is waiting for the incoming connections.
    /// </summary>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The task that resumes when the listener is started listening the socket.</returns>
    /// <exception cref="OperationCanceledException">The operation has been canceled.</exception>
    /// <exception cref="ObjectDisposedException">The listener is disposed.</exception>
    public ValueTask StartAsync(CancellationToken token = default)
    {
        if (ReferenceEquals(listener, Task.CompletedTask))
        {
            listener = ListenAsync();
        }

        return new(readiness.Task.WaitAsync(token));
    }

    /// <summary>
    /// Creates listening socket.
    /// </summary>
    /// <returns>Listening socket.</returns>
    protected abstract Socket Listen();

    /// <summary>
    /// Configures the socket associated with the incoming connection.
    /// </summary>
    /// <remarks>
    /// By default, this method does nothing.
    /// </remarks>
    /// <param name="socket">The socket that represents the incoming connection.</param>
    protected virtual void ConfigureAcceptedSocket(Socket socket)
    {
    }

    private async Task ListenAsync()
    {
        Socket listeningSocket;
        try
        {
            listeningSocket = Listen();
        }
        catch (Exception e)
        {
            readiness.TrySetException(e);
            throw;
        }

        readiness.TrySetResult();
        var headNode = default(TaskNode?);
        try
        {
            while (!lifetimeToken.IsCancellationRequested)
            {
                var clientSocket = await listeningSocket.AcceptAsync(lifetimeToken).ConfigureAwait(false);
                ConfigureAcceptedSocket(clientSocket);
                TaskNode.Add(ref headNode, DispatchAsync(clientSocket));
            }
        }
        finally
        {
            listeningSocket.Dispose();
            foreach (var task in TaskNode.GetTasks(headNode))
            {
                await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            }
        }
    }

    private void Cancel()
    {
        if (Interlocked.Exchange(ref lifetimeTokenSource, null) is { } cts)
        {
            readiness.TrySetException(new ObjectDisposedException(GetType().Name));
            try
            {
                cts.Cancel();
            }
            finally
            {
                cts.Dispose();
                backlog.Writer.Complete();
            }
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Cancel();
        }
        
        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        Cancel();
        await listener.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
    }

    /// <inheritdoc/>
    public new ValueTask DisposeAsync() => base.DisposeAsync();

    private sealed class MultiplexedStreamFactoryImpl(PipeOptions options, ChannelWriter<MultiplexedStream> backlog)
    {
        private MultiplexedStream? CreateStream(AsyncAutoResetEvent writeSignal, ref readonly TagList measurementTags)
        {
            var stream = new MultiplexedStream(options, writeSignal);
            if (backlog.TryWrite(stream))
            {
                PendingStreamCount.Add(1L, measurementTags);
            }
            else
            {
                stream = null;
            }

            return stream;
        }

        public static implicit operator MultiplexedStreamFactory(MultiplexedStreamFactoryImpl impl) => impl.CreateStream;
    }
}