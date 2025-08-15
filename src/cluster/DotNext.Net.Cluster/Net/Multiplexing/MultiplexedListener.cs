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
public abstract partial class MultiplexedListener : Disposable, IAsyncDisposable
{
    private readonly CancellationToken lifetimeToken;
    private readonly TimeSpan heartbeatTimeout, timeout;
    private readonly Channel<MultiplexedStream> backlog;
    private readonly MemoryAllocator<byte> allocator;
    private readonly int frameBufferSize, sendBufferCapacity;
    private readonly TaskCompletionSource readiness;
    private readonly Func<AsyncAutoResetEvent, MultiplexedStream?> streamFactory;
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

        allocator = configuration.BufferOptions.Pool.ToAllocator();
        streamFactory = new MultiplexedStreamFactory(configuration.BufferOptions, backlog.Writer).CreateStream;
        measurementTags = configuration.MeasurementTags;
        frameBufferSize = configuration.FrameBufferSize;
        sendBufferCapacity = configuration.SendBufferCapacity;
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

    private async Task ListenAsync()
    {
        Debugger.NotifyOfCrossThreadDependency();
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
        var connections = new HashSet<WeakReference<Task>>();
        try
        {
            while (!lifetimeToken.IsCancellationRequested)
            {
                var clientSocket = await listeningSocket.AcceptAsync(lifetimeToken).ConfigureAwait(false);
                connections.Add(new(DispatchAsync(clientSocket)));

                // GC: remove completed tasks
                connections.RemoveWhere(IsCompleted);
            }
        }
        finally
        {
            listeningSocket.Dispose();
            await Task.WhenAll(connections.Select(Unwrap)).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
            connections.Clear();
        }

        static bool IsCompleted(WeakReference<Task> taskRef)
            => !taskRef.TryGetTarget(out var task) || task.IsCompleted;

        static Task Unwrap(WeakReference<Task> taskRef)
        {
            if (!taskRef.TryGetTarget(out var task))
                task = Task.CompletedTask;

            return task;
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
}

file sealed class MultiplexedStreamFactory(PipeOptions options, ChannelWriter<MultiplexedStream> backlog)
{
    public MultiplexedStream? CreateStream(AsyncAutoResetEvent writeSignal)
    {
        var stream = new MultiplexedStream(options, writeSignal);
        return backlog.TryWrite(stream)
            ? stream
            : null;
    }
}