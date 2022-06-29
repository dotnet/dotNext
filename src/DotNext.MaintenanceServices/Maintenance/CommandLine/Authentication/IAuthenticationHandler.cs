using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine.Authentication;

using Security.Principal;
using IMaintenanceConsole = IO.IMaintenanceConsole;

/// <summary>
/// Represents authentication handler for command-line AMI.
/// </summary>
public interface IAuthenticationHandler
{
    /// <summary>
    /// Challenges the maintenance session.
    /// </summary>
    /// <param name="context">The command invocation context.</param>
    /// <param name="identity">The identity of the user to authenticate.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Authentication result; or <see langword="null"/> in case of failed authentication.</returns>
    ValueTask<IPrincipal?> ChallengeAsync(InvocationContext context, IIdentity identity, CancellationToken token);

    /// <summary>
    /// Gets global options that can be used to authenticate the command.
    /// </summary>
    /// <returns>A collection of global options.</returns>
    IEnumerable<Option> GetGlobalOptions() => Array.Empty<Option>();
}

internal static class AuthenticationHandler
{
    internal const int ForbiddenExitCode = 77; // EX_NOPERM from sysexits.h

    internal static async Task ProcessCommandAsync(this IAuthenticationHandler handler, InvocationContext context, Func<InvocationContext, Task> next)
    {
        var session = (context.Console as IMaintenanceConsole)?.Session;

        if (session?.Principal is { Identity: { IsAuthenticated: true } })
        {
            // already authenticated
            await next(context).ConfigureAwait(false);
        }
        if (session is not null && await handler.ChallengeAsync(context, session.Identity, context.GetCancellationToken()).ConfigureAwait(false) is { Identity: { IsAuthenticated: true } } principal)
        {
            // save authentication result
            session.Principal = principal;
            await next(context).ConfigureAwait(false);
        }
        else
        {
            // report error
            Forbid(context);
        }
    }

    internal static Task SetDefaultPrincipal(InvocationContext context, Func<InvocationContext, Task> next)
    {
        if ((context.Console as IMaintenanceConsole)?.Session is { Principal: null } session)
        {
            session.Principal = ReferenceEquals(AnonymousPrincipal.Instance.Identity, session.Identity)
                ? AnonymousPrincipal.Instance
                : new GenericPrincipal(session.Identity, roles: null);
        }

        return next(context);
    }

    private static void Forbid(InvocationContext context)
    {
        context.ExitCode = ForbiddenExitCode;
        context.Console.Error.Write(CommandResources.AuthenticationFailed + Environment.NewLine);
    }
}