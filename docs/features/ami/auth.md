Authentication and Authorization
====
Authentication and authorization can be enabled optionally to provide extra security layer for AMI client.

[IAuthenticationHandler](xref:DotNext.Maintenance.CommandLine.Authentication.IAuthenticationHandler) interface is an extension point to provide custom authentication mechanism. To enable authentication, the handler must be registered in AMI host:
```csharp
using DotNext.Maintenance.CommandLine;
using DotNext.Maintenance.CommandLine.Authentication;

await new HostBuilder()
    .ConfigureServices(static services =>
    {
        services
            .RegisterDefaultMaintenanceCommands()
            .UseApplicationMaintenanceInterface("/path/to/unix/domain/socket")
            .UseApplicationMaintenanceInterfaceAuthentication<CustomAuthenticationHandler>();
    })
    .Build()
    .RunAsync();
```

[PasswordAuthenticationHandler](xref:DotNext.Maintenance.CommandLine.Authentication.PasswordAuthenticationHandler) and [LinuxUdsPeerAuthenticationHandler](xref:DotNext.Maintenance.CommandLine.Authentication.LinuxUdsPeerAuthenticationHandler) handlers are provided out-of-the-box. [LinuxUdsPeerAuthenticationHandler](xref:DotNext.Maintenance.CommandLine.Authentication.LinuxUdsPeerAuthenticationHandler) allows to identify the remote peer (in other words, AMI client) connected to the socket. However, it is supported on Linux operating system only.

Authorization rules can be defined globally or at the command level. [AuthorizationCallback](xref:DotNext.Maintenance.CommandLine.Authorization.AuthorizationCallback) class is a way to define custom authorization rule.
```csharp
using DotNext.Maintenance.CommandLine;
using DotNext.Maintenance.CommandLine.Authentication;
using DotNext.Maintenance.CommandLine.Authorization;

await new HostBuilder()
    .ConfigureServices(static services =>
    {
        services
            .RegisterDefaultMaintenanceCommands()
            .UseApplicationMaintenanceInterface("/path/to/unix/domain/socket")
            .UseApplicationMaintenanceInterfaceAuthentication<CustomAuthenticationHandler>()
            .UseApplicationMaintenanceInterfaceGlobalAuthorization(static (user, cmd, ctx, token) =>
            {
                return new(user.IsInRole("role1"));
            })
    })
    .Build()
    .RunAsync();
```

`UseApplicationMaintenanceInterfaceGlobalAuthorization` extension method registers global authorization rule to be applied to all maintenance commands. [ApplicationMaintenanceCommand](xref:DotNext.Maintenance.CommandLine.ApplicationMaintenanceCommand) class has `Authorization` event that can be used to apply authorization rules for a specified maintenance command.