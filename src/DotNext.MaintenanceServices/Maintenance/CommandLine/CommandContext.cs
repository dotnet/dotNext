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
internal sealed partial class CommandContext : InvocationConfiguration
{
    public const int GenericErrorExitCode = -1;
    private const int InvalidArgumentExitCode = 64; // EX_USAGE from sysexits.h
    private const int ForbiddenExitCode = 77; // EX_NOPERM
    
    private const char LeftSquareBracket = '[';
    private const char RightSquareBracket = ']';

    private readonly IMaintenanceSession session;
    private bool printExitCode;
    private bool suppressOutputBuffer;
    private bool suppressErrorBuffer;
    
    internal CommandContext(IMaintenanceSession session)
    {
        Debug.Assert(session is not null);

        this.session = session;
        ProcessTerminationTimeout = null;
        EnableDefaultExceptionHandler = false;
    }

    internal async ValueTask<int> InvokeAsync(RootCommand root,
        string input,
        ParserConfiguration configuration,
        IAuthenticationHandler? authentication,
        AuthorizationCallback? authorization,
        CancellationToken token)
    {
        int exitCode;
        var result = root.Parse(input, configuration);
        
        if (result.Errors is { Count: > 0 } errors)
        {
            exitCode = ProcessParseErrors(root, errors, result.Tokens);
        }
        else if (await AuthenticateAsync(session, result, authentication, token).ConfigureAwait(false)
                 && await authorization.AuthorizeAsync(session, result.CommandResult, token).ConfigureAwait(false)
                 && await AuthorizeAsync(session, result.CommandResult, token).ConfigureAwait(false))
        {
            exitCode = await result.InvokeAsync(this, token).ConfigureAwait(false);
        }
        else
        {
            exitCode = Forbid(root, result.Tokens);
        }

        return exitCode;
    }

    private void ExecuteDirectives(RootCommand root, IEnumerable<Token> tokens)
    {
        var actions = from directive in root.Directives
            from token in tokens
            where Matches(token, directive)
            let action = (directive.Action as DirectiveAction)?.Action
            where action is not null
            select action;

        foreach (var action in actions)
        {
            action.Invoke(this);
        }

        static bool Matches(Token token, Directive directive)
            => token is { Type: TokenType.Directive, Value: [LeftSquareBracket, .. var directiveName, RightSquareBracket] }
               && directiveName.SequenceEqual<char>(directive.Name);
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
        => (result.Command as ApplicationMaintenanceCommand)?.AuthorizeAsync(session, result, token) ?? ValueTask.FromResult(true);

    internal void Exit(int exitCode, BufferWriter<char> output, BufferWriter<char> error)
    {
        var writer = session.ResponseWriter;
        if (printExitCode)
        {
            writer.Write(LeftSquareBracket);
            writer.Write(exitCode);
            writer.Write(RightSquareBracket);
        }

        switch (exitCode)
        {
            case 0 when suppressOutputBuffer is false:
                writer.Write(output.WrittenMemory.Span);
                break;
            case not 0 when suppressErrorBuffer is false:
                writer.Write(error.WrittenMemory.Span);
                break;
        }

        if (session.IsInteractive)
        {
            const string prompt = "> ";
            writer.Write(prompt);
        }
    }

    private int ProcessParseErrors(RootCommand root, IReadOnlyList<ParseError> errors, IReadOnlyList<Token> tokens)
    {
        ExecuteDirectives(root, tokens);
        
        foreach (var parseError in errors)
        {
            Error.WriteLine(parseError.Message);
        }

        return InvalidArgumentExitCode;
    }

    private int Forbid(RootCommand root, IReadOnlyList<Token> tokens)
    {
        ExecuteDirectives(root, tokens);
        Error.WriteLine(CommandResources.AccessDenied);
        return ForbiddenExitCode;
    }

    internal static IMaintenanceSession? TryGetSession(ParseResult result)
        => (result.InvocationConfiguration as CommandContext)?.session;
}