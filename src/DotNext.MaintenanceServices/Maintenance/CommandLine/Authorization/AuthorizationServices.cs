using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Maintenance.CommandLine.Authorization;

/// <summary>
/// Provides methods for configuring AMI authorization in DI environment.
/// </summary>
public static class AuthorizationServices
{
    /// <summary>
    /// Enables global authorization rule to be applied to all maintenance commands.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <param name="callback">Authorization callback.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationMaintenanceInterfaceGlobalAuthorization(this IServiceCollection services, AuthorizationCallback callback)
        => services.AddSingleton(callback);

    /// <summary>
    /// Enables global authorization rule to be applied to all maintenance commands.
    /// </summary>
    /// <typeparam name="TDependency">The type of the dependency.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="callbackFactory">Authorization callback factory.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationMaintenanceInterfaceGlobalAuthorization<TDependency>(this IServiceCollection services, Func<TDependency, AuthorizationCallback> callbackFactory)
        where TDependency : notnull
        => services.AddSingleton(callbackFactory.CreateCallback<TDependency>);

    private static AuthorizationCallback CreateCallback<TDependency>(this Func<TDependency, AuthorizationCallback> factory, IServiceProvider services)
        where TDependency : notnull
        => factory(services.GetRequiredService<TDependency>());
}