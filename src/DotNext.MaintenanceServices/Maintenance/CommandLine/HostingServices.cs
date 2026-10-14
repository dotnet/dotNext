using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DotNext.Maintenance.CommandLine;

using Collections.Generic;
using AuthorizationCallback = Authorization.AuthorizationCallback;
using IAuthenticationHandler = Authentication.IAuthenticationHandler;
using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;

/// <summary>
/// Provides methods for configuring AMI host in DI environment.
/// </summary>
public static class HostingServices
{
    /// <summary>
    /// Registers <see cref="IApplicationStatusProvider"/> as a singleton service
    /// and exposes access via Application Maintenance Interface.
    /// </summary>
    /// <typeparam name="TProvider">The type implementing <see cref="IApplicationStatusProvider"/>.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationStatusProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]TProvider>(this IServiceCollection services)
        where TProvider : class, IApplicationStatusProvider
    {
        return services
            .AddSingleton<IApplicationStatusProvider, TProvider>()
            .AddSingleton(CreateCommand);

        static ApplicationMaintenanceCommand CreateCommand(IServiceProvider services)
            => ApplicationMaintenanceCommand.Create(services.GetRequiredService<IApplicationStatusProvider>());
    }

    /// <summary>
    /// Registers maintenance command.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterMaintenanceCommand(this IServiceCollection services, string name, Action<ApplicationMaintenanceCommand> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton(CreateCommand);

        ApplicationMaintenanceCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationMaintenanceCommand(name);
            action(command);
            return command;
        }
    }

    /// <summary>
    /// Registers maintenance command.
    /// </summary>
    /// <typeparam name="TDependency">The command initialization dependency.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterMaintenanceCommand<TDependency>(this IServiceCollection services, string name, Action<ApplicationMaintenanceCommand, TDependency> action)
        where TDependency : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton(CreateCommand);

        ApplicationMaintenanceCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationMaintenanceCommand(name);
            action(command, services.GetRequiredService<TDependency>());
            return command;
        }
    }

    /// <summary>
    /// Registers maintenance command.
    /// </summary>
    /// <typeparam name="TDependency1">The first dependency required for command initialization.</typeparam>
    /// <typeparam name="TDependency2">The second dependency required for command initialization.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterMaintenanceCommand<TDependency1, TDependency2>(this IServiceCollection services, string name, Action<ApplicationMaintenanceCommand, TDependency1, TDependency2> action)
        where TDependency1 : notnull
        where TDependency2 : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton(CreateCommand);

        ApplicationMaintenanceCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationMaintenanceCommand(name);
            action(command, services.GetRequiredService<TDependency1>(), services.GetRequiredService<TDependency2>());
            return command;
        }
    }

    /// <summary>
    /// Registers maintenance command.
    /// </summary>
    /// <typeparam name="TDependency1">The first dependency required for command initialization.</typeparam>
    /// <typeparam name="TDependency2">The second dependency required for command initialization.</typeparam>
    /// <typeparam name="TDependency3">The third dependency required for command initialization.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterMaintenanceCommand<TDependency1, TDependency2, TDependency3>(this IServiceCollection services, string name, Action<ApplicationMaintenanceCommand, TDependency1, TDependency2, TDependency3> action)
        where TDependency1 : notnull
        where TDependency2 : notnull
        where TDependency3 : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton(CreateCommand);

        ApplicationMaintenanceCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationMaintenanceCommand(name);
            action(command, services.GetRequiredService<TDependency1>(), services.GetRequiredService<TDependency2>(), services.GetRequiredService<TDependency3>());
            return command;
        }
    }

    /// <summary>
    /// Registers default commands.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <returns>A modified registry of services.</returns>
    /// <seealso cref="ApplicationMaintenanceCommand.GetDefaultCommands"/>
    public static IServiceCollection RegisterDefaultMaintenanceCommands(this IServiceCollection services)
    {
        foreach (var command in ApplicationMaintenanceCommand.GetDefaultCommands())
            services.AddSingleton(command);

        return services;
    }

    /// <summary>
    /// Enables Application Maintenance Interface.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <param name="unixDomainSocketPath">The path to the interaction point represented by Unix Domain Socket.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationMaintenanceInterface(this IServiceCollection services, string unixDomainSocketPath)
        => services.AddSingleton<IHostedService, CommandLineMaintenanceInterfaceHost>(unixDomainSocketPath.CreateHost);

    private static CommandLineMaintenanceInterfaceHost CreateHost(this string unixDomainSocketPath, IServiceProvider services)
    {
        var host = new CommandLineMaintenanceInterfaceHost(new(unixDomainSocketPath), services.GetService<ILoggerFactory>())
        {
            Authentication = services.GetService<IAuthenticationHandler>(),
            Authorization = services.GetAuthorizationCallbacks(),
            ParserSettings = services.GetService<ParserConfiguration>(),
        };

        host.AddCommands(services.GetServices<ApplicationMaintenanceCommand>());
        return host;
    }

    /// <summary>
    /// Constructs authorization callback from multiple callbacks registered in the DI.
    /// </summary>
    /// <param name="services">A registry of services in the DI.</param>
    /// <returns>Combined authorization callback; or <see langword="null"/>.</returns>
    public static AuthorizationCallback? GetAuthorizationCallbacks(this IServiceProvider services)
    {
        var buffer = new InlineArray8<AuthorizationCallback>();
        using var owner = services.GetServices<AuthorizationCallback>().Copy(buffer);
        return owner.IsEmpty
            ? null
            : Delegate.Combine(owner.Span) as AuthorizationCallback;
    }
}