using System.CommandLine.Parsing;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine.Authorization;

/// <summary>
/// Represents authorization rule.
/// </summary>
/// <param name="principal">Authenticated user.</param>
/// <param name="target">The authorization target.</param>
/// <param name="context">Maintenance session context.</param>
/// <param name="token">The token that can be used to cancel the operation.</param>
/// <returns><see langword="true"/> if authorization passed successfully; otherwise, <see langword="false"/>.</returns>
public delegate ValueTask<bool> AuthorizationCallback(IPrincipal principal, CommandResult target, IDictionary<string, object> context, CancellationToken token);