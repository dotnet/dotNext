using System.Buffers;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.Builder;
using System.CommandLine.Help;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace DotNext.Maintenance.CommandLine;

using MaintenanceConsole = CommandLine.IO.MaintenanceConsole;

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
    private const int InvalidArgumentExitCode = 64; // EX_USAGE from sysexits.h

    private readonly Parser parser;

    /// <summary>
    /// Initializes a new host.
    /// </summary>
    /// <param name="endPoint">Unix Domain Socket address used as a interaction point.</param>
    /// <param name="commands">A set of commands to be available for execution.</param>
    /// <param name="loggerFactory">The logger factory.</param>
    public CommandLineMaintenanceInterfaceHost(UnixDomainSocketEndPoint endPoint, IEnumerable<ApplicationMaintenanceCommand> commands, ILoggerFactory? loggerFactory)
        : base(endPoint, loggerFactory)
    {
        parser = CreateCommandParser(commands);
    }

    private static Parser CreateCommandParser(IEnumerable<ApplicationMaintenanceCommand> commands)
    {
        var root = new RootCommand(RootCommand.ExecutableName + " Maintenance Interface");
        foreach (var subCommand in commands)
            root.Add(subCommand);

        return new CommandLineBuilder(root)
            .UseHelpBuilder(CustomizeHelp)
            .UseHelp()
            .UseParseErrorReporting(InvalidArgumentExitCode)
            .UseExceptionHandler(HandleException)
            .AddMiddleware(SetupServices)
            .Build();
    }

    private static void HandleException(Exception e, InvocationContext context)
    {
        switch (e)
        {
            case TimeoutException:
                const int timeoutExitCode = 75; // EX_TEMPFAIL from sysexits.h
                context.ExitCode = timeoutExitCode;
                context.Console.Error.Write(e.Message);
                break;
            default:
                if (context.Console is MaintenanceConsole console)
                {
                    console.Session.IsInteractive = false;
                    console.Session.Output.Write(e.ToString());
                }
                else
                {
                    context.Console.Error.Write(e.ToString());
                }

                break;
        }
    }

    private static Task SetupServices(InvocationContext context, Func<InvocationContext, Task> next)
    {
        var token = context.GetCancellationToken();
        context.BindingContext.AddService(Helpers.GetValueProvider(token));
        return next(context);
    }

    private static HelpBuilder CustomizeHelp(BindingContext context)
    {
        var builder = new HelpBuilder(LocalizationResources.Instance);
        builder.CustomizeLayout(DefaultLayout);
        return builder;
    }

    /// <summary>
    /// Constucts a default layout of help page.
    /// </summary>
    /// <remarks>
    /// This layout doesn't include usage syntax.
    /// </remarks>
    /// <param name="context">The help page construction context.</param>
    /// <returns>A collection of sections.</returns>
    public static IEnumerable<HelpSectionDelegate> DefaultLayout(HelpContext context)
    {
        yield return HelpBuilder.Default.SynopsisSection();
        yield return HelpBuilder.Default.CommandArgumentsSection();
        yield return HelpBuilder.Default.OptionsSection();
        yield return HelpBuilder.Default.SubcommandsSection();
        yield return HelpBuilder.Default.AdditionalArgumentsSection();
    }

    /// <summary>
    /// Gets or sets command-line parser.
    /// </summary>
    public Parser CommandParser
    {
        get => parser;
        init => parser = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <inheritdoc />
    protected override async ValueTask ExecuteCommandAsync(IMaintenanceSession session, ReadOnlyMemory<char> command, CancellationToken token)
    {
        using var console = new MaintenanceConsole(session, BufferSize, CharBufferAllocator);
        console.Exit(await parser.InvokeAsync(command.ToString(), console).ConfigureAwait(false));
    }
}