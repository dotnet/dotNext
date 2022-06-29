using System.CommandLine.Invocation;
using System.Security.Principal;

namespace DotNext.Maintenance.CommandLine.Authentication;

using LinuxUdsPeerIdentity = Security.Principal.LinuxUdsPeerIdentity;

/// <summary>
/// Represents authentication handler that expects identity of the peer connected as a client to AMI.
/// </summary>
/// <remarks>
/// This handler requires that AMI client must be represented by Unix Domain Socket remote peer on Linux.
/// </remarks>
/// <seealso cref="LinuxUdsPeerIdentity"/>
[CLSCompliant(false)]
public sealed class LinuxUdsPeerAuthenticationHandler : IAuthenticationHandler
{
    /// <inheritdoc />
    ValueTask<IPrincipal?> IAuthenticationHandler.ChallengeAsync(InvocationContext context, IIdentity identity, CancellationToken token)
        => new(identity is LinuxUdsPeerIdentity peerIdentity ? ChallengeAsync(peerIdentity) : null);

    private static GenericPrincipal ChallengeAsync(LinuxUdsPeerIdentity identity)
        => new(identity with { IsAuthenticated = true }, roles: null);
}