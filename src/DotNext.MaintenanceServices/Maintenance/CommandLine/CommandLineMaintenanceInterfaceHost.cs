using System.CommandLine;
using System.CommandLine.Invocation;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using DotNext.IO;
using Microsoft.Extensions.Logging;

namespace DotNext.Maintenance.CommandLine;

using Authentication;
using Authorization;
using Buffers;

/// <summary>
/// Provides AMI in the form of the text commands with the syntax identical to OS shell commands.
/// </summary>
/// <example>
/// <code>
/// echo "gc collect 2" | nc -U /path/to/endpoint.sock
/// </code>
/// </example>
public sealed class CommandLineMaintenanceInterfaceHost : ApplicationMaintenanceInterfaceHost
{
    private readonly RootCommand root;

    /// <summary>
    /// Initializes a new host.
    /// </summary>
    /// <param name="endPoint">Unix Domain Socket address used as an interaction point.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public CommandLineMaintenanceInterfaceHost(UnixDomainSocketEndPoint endPoint, ILoggerFactory? loggerFactory = null)
        : base(endPoint, loggerFactory)
    {
        root = new(RootCommand.ExecutableName + " Maintenance Interface")
        {
            Action = new NoOpAction()
        };

        ParserSettings = null;
        CommandContext.RegisterDirectives(root);
    }

    /// <summary>
    /// Registers maintenance commands.
    /// </summary>
    /// <param name="commands">A set of commands to register.</param>
    public void AddCommands(params IEnumerable<ApplicationMaintenanceCommand> commands)
    {
        foreach (var subCommand in commands)
            root.Add(subCommand);
    }

    /// <summary>
    /// Gets or sets authentication handler.
    /// </summary>
    public IAuthenticationHandler? Authentication
    {
        get;
        init
        {
            field = value;
            foreach (var authOption in value?.GetGlobalOptions() ?? [])
                root.Add(authOption);
        }
    }

    /// <summary>
    /// Gets or sets authorization handler.
    /// </summary>
    public AuthorizationCallback? Authorization
    {
        get;
        init;
    }

    /// <summary>
    /// Gets or sets parser settings.
    /// </summary>
    [AllowNull]
    public ParserConfiguration ParserSettings
    {
        get;
        init => field = value ?? new();
    }

    /// <inheritdoc />
    protected override async ValueTask ExecuteCommandAsync(IMaintenanceSession session, ReadOnlyMemory<char> command, CancellationToken token)
    {
        var output = new PoolingBufferWriter<char>(CharBufferAllocator) { Capacity = BufferSize };
        var error = new PoolingBufferWriter<char>(CharBufferAllocator) { Capacity = BufferSize };
        var outputWriter = TextWriter.Create(output);
        var errorWriter = TextWriter.Create(error);
        var context = new CommandContext(session) { Error = errorWriter, Output = outputWriter };
        try
        {
            var exitCode = await context
                .InvokeAsync(root, command.ToString(), ParserSettings, Authentication, Authorization, token)
                .ConfigureAwait(false);
            context.Exit(exitCode, output, error);
        }
        catch (Exception e)
        {
            session.IsInteractive = false;
            session.ResponseWriter.Write(e);
        }
        finally
        {
            output.Dispose();
            error.Dispose();
            await outputWriter.DisposeAsync().ConfigureAwait(false);
            await errorWriter.DisposeAsync().ConfigureAwait(false);
        }
    }
}

file sealed class NoOpAction : SynchronousCommandLineAction
{
    public override bool Terminating => false;

    public override int Invoke(ParseResult parseResult) => 0;
}