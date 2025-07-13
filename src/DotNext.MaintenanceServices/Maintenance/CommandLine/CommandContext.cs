using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine;

using Authentication;
using Authorization;
using Buffers;

/// <summary>
/// Represents command invocation context.
/// </summary>
public sealed partial class CommandContext : CommandLineConfiguration
{
    private const int InvalidArgumentExitCode = 64; // EX_USAGE from sysexits.h
    private const int ForbiddenExitCode = 77; // EX_NOPERM
    
    private bool printExitCode;
    private bool suppressOutputBuffer;
    private bool suppressErrorBuffer;
    
    internal CommandContext(RootCommand root, IMaintenanceSession session)
        : base(root)
    {
        Debug.Assert(root is not null);
        Debug.Assert(session is not null);

        Session = session;
        ProcessTerminationTimeout = null;
        EnableDefaultExceptionHandler = false;
    }

    /// <summary>
    /// Gets the maintenance session.
    /// </summary>
    public IMaintenanceSession Session { get; }

    internal async ValueTask<int> InvokeAsync(string input,
        IAuthenticationHandler? authentication,
        AuthorizationCallback? authorization,
        CancellationToken token)
    {
        int exitCode;
        var result = Parse(input);
        
        if (result.Errors is { Count: > 0 } errors)
        {
            exitCode = ProcessParseErrors(errors);
        }
        else if (await AuthenticateAsync(Session, result, authentication, token).ConfigureAwait(false)
                 && await authorization.AuthorizeAsync(Session, result.CommandResult, token).ConfigureAwait(false)
                 && await AuthorizeAsync(Session, result.CommandResult, token).ConfigureAwait(false))
        {
            exitCode = await result.InvokeAsync(token).ConfigureAwait(false);
        }
        else
        {
            ExecuteDirectives(result);
            exitCode = Forbid();
        }

        return exitCode;
    }

    private void ExecuteDirectives(ParseResult result)
    {
        var directives = (RootCommand as RootCommand)?.Directives ?? [];
        foreach (var token in result.Tokens)
        {
            if (token.Type is TokenType.Directive
                && directives.FirstOrDefault(d => Matches(token, d))?.Action is DirectiveAction action)
            {
                action.Action.Invoke(this);
            }
        }

        static bool Matches(Token token, Directive directive)
        {
            ReadOnlySpan<char> tokenValue = token.Value;
            return !tokenValue.IsEmpty
                   && tokenValue[0] is '['
                   && tokenValue[^1] is ']'
                   && tokenValue[1..^1].SequenceEqual(directive.Name);
        }
    }

    private static async ValueTask<bool> AuthenticateAsync(IMaintenanceSession session,
        ParseResult result,
        IAuthenticationHandler? authentication, 
        CancellationToken token)
    {
        if (session.Principal is { Identity.IsAuthenticated: true })
        {
            // do nothing
        }
        else if (authentication is null)
        {
            session.Principal = new GenericPrincipal(session.Identity, roles: null);
        }
        else if (await authentication.ChallengeAsync(result, session.Identity, token).ConfigureAwait(false) is { } principal)
        {
            session.Principal = principal;
        }
        else
        {
            return false;
        }

        return true;
    }
    
    private static ValueTask<bool> AuthorizeAsync(IMaintenanceSession session,
        CommandResult result,
        CancellationToken token)
        => result.Command is ApplicationMaintenanceCommand command
            ? command.AuthorizeAsync(session, result, token)
            : ValueTask.FromResult(true);

    internal void Exit(int exitCode, BufferWriter<char> output, BufferWriter<char> error)
    {
        if (printExitCode)
        {
            Session.ResponseWriter.Write('[');
            Session.ResponseWriter.Write(exitCode);
            Session.ResponseWriter.Write(']');
        }

        switch (exitCode)
        {
            case 0 when suppressOutputBuffer is false:
                Session.ResponseWriter.Write(output.WrittenMemory.Span);
                break;
            case not 0 when suppressErrorBuffer is false:
                Session.ResponseWriter.Write(error.WrittenMemory.Span);
                break;
        }

        if (Session.IsInteractive)
        {
            const string prompt = "> ";
            Session.ResponseWriter.Write(prompt);
        }
    }

    private int ProcessParseErrors(IReadOnlyList<ParseError> parseErrors)
    {
        foreach (var parseError in parseErrors)
        {
            Error.WriteLine(parseError.Message);
        }

        return InvalidArgumentExitCode;
    }

    private int Forbid()
    {
        Output.WriteLine(CommandResources.AccessDenied);
        return ForbiddenExitCode;
    }
}