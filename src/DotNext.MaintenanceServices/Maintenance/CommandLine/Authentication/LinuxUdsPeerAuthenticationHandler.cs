using System.CommandLine;
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
    ValueTask<IPrincipal?> IAuthenticationHandler.ChallengeAsync(ParseResult context, IIdentity identity, CancellationToken token)
        => new(identity is LinuxUdsPeerIdentity peerIdentity ? Challenge(peerIdentity) : null);

    private static GenericPrincipal Challenge(LinuxUdsPeerIdentity identity)
        => new(identity with { IsAuthenticated = true }, roles: null);
}