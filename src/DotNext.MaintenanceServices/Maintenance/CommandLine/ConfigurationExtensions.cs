using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DotNext.Maintenance.CommandLine;

using IApplicationStatusProvider = Diagnostics.IApplicationStatusProvider;

/// <summary>
/// Provides configuration helpers for DI environment.
/// </summary>
public static class ConfigurationExtensions
{
    /// <summary>
    /// Registers <see cref="IApplicationStatusProvider"/> as a singleton service
    /// and exposes access via Application Management Interface.
    /// </summary>
    /// <typeparam name="TProvider">The type implementing <see cref="IApplicationStatusProvider"/>.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationStatusProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IApplicationStatusProvider
    {
        return services
            .AddSingleton<IApplicationStatusProvider, TProvider>()
            .AddSingleton<ApplicationManagementCommand>(CreateCommand);

        static ApplicationManagementCommand CreateCommand(IServiceProvider services)
            => services.GetRequiredService<IApplicationStatusProvider>().CreateCommand();
    }

    /// <summary>
    /// Registers management command.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterCommand(this IServiceCollection services, string name, Action<ApplicationManagementCommand> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton<ApplicationManagementCommand>(CreateCommand);

        ApplicationManagementCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationManagementCommand(name);
            action(command);
            return command;
        }
    }

    /// <summary>
    /// Registers management command.
    /// </summary>
    /// <typeparam name="TDependency">The command initialization dependency.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterCommand<TDependency>(this IServiceCollection services, string name, Action<ApplicationManagementCommand, TDependency> action)
        where TDependency : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton<ApplicationManagementCommand>(CreateCommand);

        ApplicationManagementCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationManagementCommand(name);
            action(command, services.GetRequiredService<TDependency>());
            return command;
        }
    }

    /// <summary>
    /// Registers management command.
    /// </summary>
    /// <typeparam name="TDependency1">The first dependency required for command initialization.</typeparam>
    /// <typeparam name="TDependency2">The second dependency required for command initialization.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterCommand<TDependency1, TDependency2>(this IServiceCollection services, string name, Action<ApplicationManagementCommand, TDependency1, TDependency2> action)
        where TDependency1 : notnull
        where TDependency2 : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton<ApplicationManagementCommand>(CreateCommand);

        ApplicationManagementCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationManagementCommand(name);
            action(command, services.GetRequiredService<TDependency1>(), services.GetRequiredService<TDependency2>());
            return command;
        }
    }

    /// <summary>
    /// Registers management command.
    /// </summary>
    /// <typeparam name="TDependency1">The first dependency required for command initialization.</typeparam>
    /// <typeparam name="TDependency2">The second dependency required for command initialization.</typeparam>
    /// <typeparam name="TDependency3">The third dependency required for command initialization.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="name">The name of the command.</param>
    /// <param name="action">The action that can be used to initialize the command.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection RegisterCommand<TDependency1, TDependency2, TDependency3>(this IServiceCollection services, string name, Action<ApplicationManagementCommand, TDependency1, TDependency2, TDependency3> action)
        where TDependency1 : notnull
        where TDependency2 : notnull
        where TDependency3 : notnull
    {
        ArgumentNullException.ThrowIfNull(action);
        return services.AddSingleton<ApplicationManagementCommand>(CreateCommand);

        ApplicationManagementCommand CreateCommand(IServiceProvider services)
        {
            var command = new ApplicationManagementCommand(name);
            action(command, services.GetRequiredService<TDependency1>(), services.GetRequiredService<TDependency2>(), services.GetRequiredService<TDependency3>());
            return command;
        }
    }

    /// <summary>
    /// Enables Application Management Interface.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <param name="unixDomainSocketPath">The path to the interaction point represented by Unix Domain Socket.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationManagementInterface(this IServiceCollection services, string unixDomainSocketPath)
        => services.AddSingleton<IHostedService, CommandLineManagementInterfaceHost>(unixDomainSocketPath.CreateHost);

    private static CommandLineManagementInterfaceHost CreateHost(this string unixDomainSocketPath, IServiceProvider services)
        => new(new(unixDomainSocketPath), services.GetServices<ApplicationManagementCommand>());

    /// <summary>
    /// Enables Application Management Interface.
    /// </summary>
    /// <param name="services">A registry of services.</param>
    /// <param name="hostFactory">The host factory.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationManagementInterface(this IServiceCollection services, Func<IEnumerable<ApplicationManagementCommand>, CommandLineManagementInterfaceHost> hostFactory)
        => services.AddSingleton<IHostedService, CommandLineManagementInterfaceHost>(hostFactory.CreateHost);

    private static CommandLineManagementInterfaceHost CreateHost(this Func<IEnumerable<ApplicationManagementCommand>, CommandLineManagementInterfaceHost> hostFactory, IServiceProvider services)
        => hostFactory(services.GetServices<ApplicationManagementCommand>());

    /// <summary>
    /// Enables Application Management Interface.
    /// </summary>
    /// <typeparam name="TDependency">The dependency required for host initialization.</typeparam>
    /// <param name="services">A registry of services.</param>
    /// <param name="hostFactory">The host factory.</param>
    /// <returns>A modified registry of services.</returns>
    public static IServiceCollection UseApplicationManagementInterface<TDependency>(this IServiceCollection services, Func<IEnumerable<ApplicationManagementCommand>, TDependency, CommandLineManagementInterfaceHost> hostFactory)
        where TDependency : notnull
        => services.AddSingleton<IHostedService, CommandLineManagementInterfaceHost>(hostFactory.CreateHost);

    private static CommandLineManagementInterfaceHost CreateHost<TDependency>(this Func<IEnumerable<ApplicationManagementCommand>, TDependency, CommandLineManagementInterfaceHost> hostFactory, IServiceProvider services)
        where TDependency : notnull
        => hostFactory(services.GetServices<ApplicationManagementCommand>(), services.GetRequiredService<TDependency>());
}