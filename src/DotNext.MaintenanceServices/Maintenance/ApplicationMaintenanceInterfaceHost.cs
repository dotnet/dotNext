using System.Buffers;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNext.Maintenance;

using Buffers;
using Security.Principal;
using static Runtime.InteropServices.UnixDomainSocketInterop;
using EncodingContext = Text.EncodingContext;
using NullLogger = Microsoft.Extensions.Logging.Abstractions.NullLogger;

/// <summary>
/// Represents host of Application Maintenance Interface (AMI). The host provides
/// IPC using Unix Domain Socket.
/// </summary>
/// <remarks>
/// AMI (Application Maintenance Interface) allows to interact with running
/// application or microservice through Unix Domain Socket.
/// The administrator can use command-line tools
/// such as netcat to send commands to the applications. These commands may trigger GC,
/// clear application cache, force reconnection to DB or any other maintenance actions.
/// </remarks>
public abstract class ApplicationMaintenanceInterfaceHost : BackgroundService
{
    private const int MinBufferSize = 32;
    private static readonly ReadOnlyMemory<char> NewLineMemory = Environment.NewLine.AsMemory();
    private static readonly ReadOnlyMemory<char> NullTerminationCharMemory = new[] { '\0' };

    private readonly UnixDomainSocketEndPoint endPoint;
    private readonly int backlog = 5;
    private readonly int bufferSize = 512;
    private readonly Encoding encoding = Encoding.UTF8;

    /// <summary>
    /// Initializes a new host.
    /// </summary>
    /// <param name="endPoint">Unix Domain Socket address used as a interaction point.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    /// <exception cref="ArgumentNullException"><paramref name="endPoint"/> is <see langword="null"/>.</exception>
    protected ApplicationMaintenanceInterfaceHost(UnixDomainSocketEndPoint endPoint, ILoggerFactory? loggerFactory)
    {
        this.endPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
        Logger = loggerFactory?.CreateLogger(GetType()) ?? NullLogger.Instance;
    }

    /// <summary>
    /// Gets the logger associated with this host.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Gets or sets the maximum length of the pending connections queue.
    /// </summary>
    public int Backlog
    {
        get => backlog;
        init => backlog = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the internal buffer size.
    /// </summary>
    public int BufferSize
    {
        get => bufferSize;
        init => bufferSize = value >= MinBufferSize ? value : throw new ArgumentOutOfRangeException(nameof(value));
    }

    /// <summary>
    /// Gets or sets allocator for the buffer of bytes.
    /// </summary>
    public MemoryAllocator<byte>? ByteBufferAllocator
    {
        get;
        init;
    }

    /// <summary>
    /// Gets or sets allocator for the buffer of characters.
    /// </summary>
    public MemoryAllocator<char>? CharBufferAllocator
    {
        get;
        init;
    }

    /// <summary>
    /// Gets or sets encoding of the command text and responses.
    /// </summary>
    public Encoding TextEncoding
    {
        get => encoding;
        init => encoding = value ?? throw new ArgumentNullException(nameof(value));
    }

    // detects new line chars sequence or null char
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private static async ValueTask<int> ReadRequestAsync(Socket clientSocket, Encoding encoding, Memory<byte> buffer, BufferWriter<char> output, CancellationToken token)
    {
        var decoder = encoding.GetDecoder();
        int bytesRead;

        while ((bytesRead = await clientSocket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false)) > 0)
        {
            var charsWritten = encoding.GetMaxCharCount(bytesRead);
            charsWritten = decoder.GetChars(buffer.Span.Slice(0, bytesRead), output.GetSpan(charsWritten), flush: false);
            output.Advance(charsWritten);

            var indexOfLineTerm = GetLineTerminationPosition(output.WrittenMemory.Span);
            if (indexOfLineTerm >= 0)
                return indexOfLineTerm;
        }

        return output.WrittenCount;

        static int GetLineTerminationPosition(ReadOnlySpan<char> input)
        {
            var newLineIndex = input.IndexOf(NewLineMemory.Span, StringComparison.Ordinal);
            var nullTermIndex = input.IndexOf(NullTerminationCharMemory.Span, StringComparison.Ordinal);

            if (newLineIndex < 0)
                return nullTermIndex;

            if (nullTermIndex < 0)
                return newLineIndex;

            return Math.Min(newLineIndex, nullTermIndex);
        }
    }

    /// <summary>
    /// Executes command asynchronously.
    /// </summary>
    /// <param name="session">Command session.</param>
    /// <param name="command">The command to execute.</param>
    /// <param name="token">The token that is associated with the host lifetime.</param>
    /// <returns>A task representing asynchronous execution of the command.</returns>
    protected abstract ValueTask ExecuteCommandAsync(IMaintenanceSession session, ReadOnlyMemory<char> command, CancellationToken token);

    /// <summary>
    /// Identifies the requester asynchronously.
    /// </summary>
    /// <param name="socket">The socket connected with the client side.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>The identity of the requester.</returns>
    protected virtual ValueTask<IIdentity> IdentifyAsync(Socket socket, CancellationToken token)
    {
        IIdentity result;
        if (OperatingSystem.IsLinux() && socket.TryGetCredentials(out var processId, out var userId, out var groupId))
        {
            result = new LinuxUdsPeerIdentity()
            {
                ProcessId = processId,
                UserId = userId,
                GroupId = groupId,
            };
        }
        else
        {
            result = IMaintenanceSession.AnonymousIdentity;
        }

        return new(result);
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    private static async ValueTask WriteResponseAsync(Socket clientSocket, ReadOnlyMemory<char> response, EncodingContext encoding, Memory<byte> buffer, CancellationToken token)
    {
        foreach (var chunk in encoding.GetBytes(response, buffer))
        {
            for (var offset = 0; offset < chunk.Length;)
            {
                offset += await clientSocket.SendAsync(chunk, SocketFlags.None, token).ConfigureAwait(false);
            }
        }
    }

    private async void ProcessRequestAsync(Socket clientSocket, CancellationToken token)
    {
        var buffer = default(MemoryOwner<byte>);
        var session = default(Session);
        var inputBuffer = default(BufferWriter<char>);
        try
        {
            // process request
            buffer = ByteBufferAllocator.Invoke(bufferSize, exactSize: false);
            session = new Session(bufferSize, CharBufferAllocator, await IdentifyAsync(clientSocket, token).ConfigureAwait(false));
            inputBuffer = new PooledBufferWriter<char>
            {
                BufferAllocator = CharBufferAllocator,
                Capacity = bufferSize,
            };

            while (true)
            {
                var commandLength = await ReadRequestAsync(clientSocket, encoding, buffer.Memory, inputBuffer, token).ConfigureAwait(false);

                // skip empty input
                if (commandLength > 0)
                {
                    await ExecuteCommandAsync(session, inputBuffer.WrittenMemory.Slice(0, commandLength), token).ConfigureAwait(false);
                    await WriteResponseAsync(clientSocket, session.Output, encoding, buffer.Memory, token).ConfigureAwait(false);
                }

                if (session.IsInteractive)
                {
                    session.ReuseOutputBuffer();
                    inputBuffer.Clear(reuseBuffer: true);
                }
                else
                {
                    break;
                }
            }

            await clientSocket.DisconnectAsync(reuseSocket: false, token).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == token)
        {
            // suppresss cancellation
        }
        catch (SocketException e) when (e.SocketErrorCode is SocketError.Shutdown)
        {
            // suppress shutdown by client
        }
        catch (Exception e)
        {
            Logger.FailedToProcessCommand(endPoint, e);
        }
        finally
        {
            clientSocket.Dispose();
            inputBuffer?.Dispose();
            buffer.Dispose();
            session?.Dispose();
        }
    }

    private void ProcessRequestAsync(Tuple<Socket, CancellationToken> args)
        => ProcessRequestAsync(args.Item1, args.Item2);

    /// <summary>
    /// Starts listening for commands to be received through Unix Domain Socket.
    /// </summary>
    /// <param name="token">The token associated with the lifetime of this host.</param>
    /// <returns>The task representing command processing loop.</returns>
    protected sealed override async Task ExecuteAsync(CancellationToken token)
    {
        using var listener = new Socket(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
        listener.Bind(endPoint);
        listener.Listen(backlog);

        // preallocate delegate
        Action<Tuple<Socket, CancellationToken>> requestProcessor = ProcessRequestAsync;

        while (!token.IsCancellationRequested)
        {
            var connection = await listener.AcceptAsync(token).ConfigureAwait(false);
            ThreadPool.QueueUserWorkItem(requestProcessor, new Tuple<Socket, CancellationToken>(connection, token), preferLocal: false);
        }
    }

    private sealed class Session : Disposable, IMaintenanceSession
    {
        private readonly PooledBufferWriter<char> output;
        private IDictionary<string, object>? context;
        private object? identityOrPrincipal;

        internal Session(int capacity, MemoryAllocator<char>? allocator, IIdentity identity)
        {
            output = new() { BufferAllocator = allocator, Capacity = capacity };
            identityOrPrincipal = identity;
        }

        IIdentity IMaintenanceSession.Identity => identityOrPrincipal switch
        {
            IIdentity id => id,
            IPrincipal principal => principal.Identity ?? IMaintenanceSession.AnonymousIdentity,
            _ => IMaintenanceSession.AnonymousIdentity,
        };

        IPrincipal? IMaintenanceSession.Principal
        {
            get => identityOrPrincipal as IPrincipal;
            set => identityOrPrincipal = value;
        }

        internal void ReuseOutputBuffer() => output.Clear(reuseBuffer: true);

        internal ReadOnlyMemory<char> Output => output.WrittenMemory;

        public bool IsInteractive { get; set; }

        IBufferWriter<char> IMaintenanceSession.Output => output;

        public IDictionary<string, object> Context => context ??= new Dictionary<string, object>(StringComparer.Ordinal);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                output.Dispose();
                context?.Clear();
            }

            base.Dispose(disposing);
        }
    }
}