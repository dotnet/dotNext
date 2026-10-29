using System.Globalization;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNext.Maintenance;

using Buffers;
using Collections.Specialized;
using IO;
using Runtime.CompilerServices;
using Security.Principal;
using static Runtime.InteropServices.UnixDomainSocketInterop;
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
public abstract class ApplicationMaintenanceInterfaceHost : Disposable, IHostedService
{
    private const int MinBufferSize = 32;
    internal const UnixFileMode DefaultSocketFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite;
    private static readonly ReadOnlyMemory<char> NewLineMemory = Environment.NewLine.AsMemory();
    private static readonly ReadOnlyMemory<char> NullTerminationCharMemory = new[] { '\0' };

    private readonly UnixDomainSocketEndPoint endPoint;
    private readonly Socket listener;
    private readonly CancellationTokenSource stoppingTokenSource;
    private readonly CancellationToken stoppingToken;
    private Task listenerTask;

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
        listener = new(endPoint.AddressFamily, SocketType.Stream, ProtocolType.Unspecified);
        stoppingTokenSource = new();
        stoppingToken = stoppingTokenSource.Token;
        listenerTask = Task.CompletedTask;
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
        get;
        init => field = value > 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = 5;

    /// <summary>
    /// Gets or sets the internal buffer size.
    /// </summary>
    public int BufferSize
    {
        get;
        init => field = value >= MinBufferSize ? value : throw new ArgumentOutOfRangeException(nameof(value));
    } = Environment.SystemPageSize;

    /// <summary>
    /// Gets or sets allocator for the buffer of bytes.
    /// </summary>
    public MemoryAllocator<byte> ByteBufferAllocator
    {
        get;
        init => field = value.DefaultIfNull;
    } = MemoryAllocator<byte>.Default;

    /// <summary>
    /// Gets or sets allocator for the buffer of characters.
    /// </summary>
    public MemoryAllocator<char> CharBufferAllocator
    {
        get;
        init => field = value.DefaultIfNull;
    } = MemoryAllocator<char>.Default;

    /// <summary>
    /// Gets or sets encoding of the command text and responses.
    /// </summary>
    public Encoding TextEncoding
    {
        get;
        init => field = value ?? throw new ArgumentNullException(nameof(value));
    } = Encoding.UTF8;

    /// <summary>
    /// Gets or sets access permissions of the socket file.
    /// </summary>
    /// <remarks>
    /// By default, only the processes running as the owner of the socket file or as a member of its group
    /// (and privileged processes such as root) can connect to the socket. Use <see cref="UnixFileMode.UserRead"/> | <see cref="UnixFileMode.UserWrite"/>
    /// to restrict access to the processes running with the same effective user ID only.
    /// The permissions are applied before the host starts accepting connections.
    /// The property is ignored on Windows and for abstract socket addresses.
    /// </remarks>
    [UnsupportedOSPlatform("windows")]
    public UnixFileMode SocketFileMode { get; init; } = DefaultSocketFileMode;

    // detects new line chars sequence or null char
    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    private static async ValueTask<int> ReadRequestAsync(Socket clientSocket, Encoding encoding, Decoder decoder, Memory<byte> buffer, BufferWriter<char> output, CancellationToken token)
    {
        for (int bytesRead; (bytesRead = await clientSocket.ReceiveAsync(buffer, SocketFlags.None, token).ConfigureAwait(false)) > 0;)
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

            return int.Min(newLineIndex, nullTermIndex);
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

    private static IIdentity GetRemotePeerIdentity(Socket socket)
    {
        if (OperatingSystem.IsLinux() && socket.TryGetCredentials(out var processId, out var userId, out var groupId))
        {
            return new LinuxUdsPeerIdentity(processId, userId, groupId);
        }

        return AnonymousPrincipal.Instance;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ProcessRequestAsync(Socket clientSocket)
    {
        var session = default(MaintenanceSession);
        var inputBuffer = default(BufferWriter<char>);
        var outputBuffer = default(BufferWriter<byte>);
        try
        {
            outputBuffer = new PoolingBufferWriter<byte>(ByteBufferAllocator)
            {
                Capacity = BufferSize,
            };
            inputBuffer = new PoolingBufferWriter<char>(CharBufferAllocator)
            {
                Capacity = BufferSize,
            };

            session = new(clientSocket, TextEncoding, outputBuffer, GetRemotePeerIdentity(clientSocket));
            for (var decoder = TextEncoding.GetDecoder(); ; decoder.Reset())
            {
                int commandLength;

                using (var buffer = ByteBufferAllocator.AllocateAtLeast(BufferSize))
                {
                    commandLength = await ReadRequestAsync(clientSocket, TextEncoding, decoder, buffer.Memory, inputBuffer, stoppingToken).ConfigureAwait(false);
                }

                // skip empty input
                if (commandLength > 0)
                {
                    await ExecuteCommandAsync(session, inputBuffer.WrittenMemory.Slice(0, commandLength), stoppingToken).ConfigureAwait(false);
                }

                await clientSocket.FlushAsync(outputBuffer, stoppingToken).ConfigureAwait(false);
                if (session.IsInteractive)
                {
                    inputBuffer.Clear(reuseBuffer: true);
                }
                else
                {
                    break;
                }
            }

            await clientSocket.DisconnectAsync(reuseSocket: false, stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException e) when (e.CancellationToken == stoppingToken)
        {
            // suppress cancellation
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
            outputBuffer?.Dispose();
            session?.Dispose();
        }
    }

    /// <inheritdoc/>
    public Task StartAsync(CancellationToken token)
    {
        var task = Task.CompletedTask;
        try
        {
            listener.Bind(endPoint);

            // connect() is refused until listen(), so there is no window with the default permissions
            if (!OperatingSystem.IsWindows() && endPoint.ToString() is [not '@', ..] path)
            {
                File.SetUnixFileMode(path, SocketFileMode);
            }

            listener.Listen(Backlog);
        }
        catch (Exception e)
        {
            task = Task.FromException(e);
        }

        listenerTask = ListenAsync();
        return task;
    }

    [AsyncMethodBuilder(typeof(SpawningAsyncTaskMethodBuilder))]
    private async Task ListenAsync()
    {
        // preallocate delegate
        while (!stoppingToken.IsCancellationRequested)
        {
            var connection = await listener.AcceptAsync(stoppingToken).ConfigureAwait(false);
            _ = ProcessRequestAsync(connection);
        }
    }

    /// <inheritdoc/>
    public async Task StopAsync(CancellationToken token)
    {
        await stoppingTokenSource.CancelAsync().ConfigureAwait(false);
        await listenerTask.WaitAsync(token).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        listenerTask = Task.CompletedTask;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            stoppingTokenSource.Dispose();
            listener.Dispose();
        }
        
        base.Dispose(disposing);
    }

    private sealed class MaintenanceSession : TypeMap, IMaintenanceSession
    {
        private object? identityOrPrincipal;

        internal MaintenanceSession(Socket socket, Encoding encoding, BufferWriter<byte> buffer, IIdentity identity)
        {
            ResponseWriter = TextWriter.Create(buffer, encoding, CultureInfo.CurrentCulture, socket.Flush, socket.FlushAsync);
            identityOrPrincipal = identity;
        }

        IIdentity IMaintenanceSession.Identity => identityOrPrincipal switch
        {
            IIdentity id => id,
            IPrincipal principal => principal.Identity ?? AnonymousPrincipal.Instance,
            _ => AnonymousPrincipal.Instance,
        };

        IPrincipal? IMaintenanceSession.Principal
        {
            get => identityOrPrincipal as IPrincipal;
            set => identityOrPrincipal = value;
        }

        public bool IsInteractive { get; set; }

        public TextWriter ResponseWriter { get; }

        ITypeMap IMaintenanceSession.Context => this;

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                ResponseWriter.Dispose();
                Clear();
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }

        ~MaintenanceSession() => Dispose(disposing: false);
    }
}