using System.CommandLine;
using System.CommandLine.Invocation;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine.Authentication;

/// <summary>
/// Represents authentication handler for command-line AMI.
/// </summary>
public interface IAuthenticationHandler
{
    /// <summary>
    /// Challenges the maintenance session.
    /// </summary>
    /// <param name="result">The parsing result.</param>
    /// <param name="identity">The identity of the user to authenticate.</param>
    /// <param name="token">The token that can be used to cancel the operation.</param>
    /// <returns>Authentication result; or <see langword="null"/> in case of failed authentication.</returns>
    ValueTask<IPrincipal?> ChallengeAsync(ParseResult result, IIdentity identity, CancellationToken token);

    /// <summary>
    /// Gets global options that can be used to authenticate the command.
    /// </summary>
    /// <returns>A collection of global options.</returns>
    IEnumerable<Option> GetGlobalOptions() => [];
}