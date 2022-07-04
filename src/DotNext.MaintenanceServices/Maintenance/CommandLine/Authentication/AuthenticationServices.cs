using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Maintenance.CommandLine.Authentication;

/// <summary>
/// Provides methods for configuring AMI authentication in DI environment.
/// </summary>
public static class AuthenticationServices
{
    /// <summary>
    /// Enables authentication of the specified type required to execute commands over AMI.
    /// </summary>
    /// <typeparam name="T">The type of the authentication.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <returns>A modified registry of services.</returns>
    /// <seealso cref="PasswordAuthenticationHandler"/>
    /// <seealso cref="LinuxUdsPeerAuthenticationHandler"/>
    public static IServiceCollection UseApplicationMaintenanceInterfaceAuthentication<T>(this IServiceCollection services)
        where T : class, IAuthenticationHandler
        => services.AddSingleton<IAuthenticationHandler, T>();
}