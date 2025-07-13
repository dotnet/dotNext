using System.CommandLine;
using System.CommandLine.Invocation;
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
    private readonly IAuthenticationHandler? authentication;
    private readonly AuthorizationCallback? authorization;

    /// <summary>
    /// Initializes a new host.
    /// </summary>
    /// <param name="endPoint">Unix Domain Socket address used as a interaction point.</param>
    /// <param name="commands">A set of commands to be available for execution.</param>
    /// <param name="authentication">Optional authentication handler.</param>
    /// <param name="authorization">A set of global authorization rules to be applied to all commands.</param>
    /// <param name="loggerFactory">Optional logger factory.</param>
    public CommandLineMaintenanceInterfaceHost(
        UnixDomainSocketEndPoint endPoint,
        IEnumerable<ApplicationMaintenanceCommand> commands,
        IAuthenticationHandler? authentication = null,
        AuthorizationCallback? authorization = null,
        ILoggerFactory? loggerFactory = null)
        : base(endPoint, loggerFactory)
    {
        root = new RootCommand(RootCommand.ExecutableName + " Maintenance Interface");
        root.Action = new MyAction();
        foreach (var subCommand in commands)
            root.Add(subCommand);

        foreach (var authOption in authentication?.GetGlobalOptions() ?? [])
            root.Add(authOption);
        
        CommandContext.RegisterDirectives(root);

        this.authentication = authentication;
        this.authorization = authorization;
    }
    
    private sealed class MyAction : SynchronousCommandLineAction
    {
        public MyAction()
        {
            Terminating = false;
        }
        
        public override int Invoke(ParseResult parseResult)
        {
            return 0;
        }
    }

    /// <inheritdoc />
    protected override async ValueTask ExecuteCommandAsync(IMaintenanceSession session, ReadOnlyMemory<char> command, CancellationToken token)
    {
        var output = new PoolingBufferWriter<char>(CharBufferAllocator) { Capacity = BufferSize };
        var error = new PoolingBufferWriter<char>(CharBufferAllocator) { Capacity = BufferSize };
        var outputWriter = output.AsTextWriter();
        var errorWriter = error.AsTextWriter();
        var context = new CommandContext(root, session) { Error = errorWriter, Output = outputWriter };
        try
        {
            var exitCode = await context.InvokeAsync(command.ToString(), authentication, authorization, token).ConfigureAwait(false);
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