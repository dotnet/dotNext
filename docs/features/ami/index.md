Application Maintenance Interface
====
.NET ecosystem provides a rich set of tools for collecting runtime metrics and diagnostics information. However, this is one-way communication with the application. What if administrator wants to trigger Garbage Collector or clear cache? **Application Maintenance Interface** (AMI) provides a way to send maintenance commands to .NET application directly.

AMI is a sort of Inter-Process Communication specially designed to have first-class support by command-line shells such as Bash. The implementation is constructed on top of open technologies:
* IPC transport layer is based on Unix Domain Sockets
* Input is just a plain text
* The format of the input follows convention of POSIX or Windows command-line tools

In other words, AMI is very similar to Telnet or SSH.

> [!NOTE]
> AMI is secure, because there is no way to establish connection to the application remotely. Unix Domain Socket is a form of IPC without support of connection through the network. However, administrator can still use SSH to connect to the host or container and then execute command via command-line shell using, for instance, _netcat_.

AMI can be seen as alternative to [Java Management Extensions](https://docs.oracle.com/javase/8/docs/technotes/guides/jmx/index.html) but built natively for .NET and based on open technologies. The following table describes the difference between these technologies:

| Aspect | JMX | AMI |
| ---- | ---- | ---- |
| Data format | Binary (Java serialization) | Plain text |
| Direct remote access | Yes | No |
| Requires special client app | Yes (for instance, VisualVM) | No |
| Built-in support of Kubernetes probes | No | Yes |

AMI provides the following maintenace commands out-of-the-box:
* `gc` - forces Garbage Collection or sets compaction mode for LOH
* `interactive-mode` - enters interactive mode. This mode is suitable for administrators
* `exit` - leaves interactive mode
* `probe` - execute application probe. See [here](./probes.md) for more information

At the application startup, AMI infrastructure creates interaction point represented by pseudo file of Unix Domain Socket type. This path can be used by tools like `nc` for connection from the terminal session:
```sh
nc -U /path/to/unix/domain/socket
```

For instance, you can force Garbage Collection directly from the command-line shell:
```sh
echo gc collect 2 | nc -U /path/to/unix/domain/socket
```

If you want to send the command programmatically then you need to insert line termination sequence (`\n` on Unix, or `\n\r` on Windows) or null character (`\0`) in the end of each command.

Some of the commands may produce output in the form of plain text.

In interactive mode, administrator can use standard flags `-h` or `--help` to get help for the commands or subcommands.

# AMI Hosting
AMI is supported for the application with or without Dependency Injection support. For simplicity, all code examples implies DI enabled.

The following code snippet demonstrates bare minimum to enable AMI for the application:
```csharp
using DotNext.Maintenance.CommandLine;

await new HostBuilder()
    .ConfigureServices(static services =>
    {
        services
            .RegisterDefaultMaintenanceCommands()
            .UseApplicationMaintenanceInterface("/path/to/unix/domain/socket");
    })
    .Build()
    .RunAsync();
```

`RegisterDefaultMaintenanceCommands` extension method registers default set of maintenance commands (`gc`, `interactive-mode`, `exit`). `UseApplicationMaintenanceInterface` registers a host that listens for the commands to be received through Unix Domain Socket at the specificed location. Note that pseudo file at that path **should not** exist before application startup. The file will be created by the host automatically. However, if file exists, the host throws an exception.

# Custom Commands
Command parsing is implemented on top of [System.CommandLine](https://docs.microsoft.com/en-us/dotnet/standard/commandline/) open-source library. [ApplicationMaintenanceCommand](xref:DotNext.Maintenance.CommandLine.ApplicationMaintenanceCommand) class represents maintenance command that can be registered in DI:
```csharp
using System.Buffers;
using System.CommandLine;
using System.CommandLine.Parsing;
using DotNext.Maintenance.CommandLine;
using DotNext.Maintenance.CommandLine.Binding;
using Microsoft.Extensions.Hosting;

await new HostBuilder()
    .ConfigureServices(static services =>
    {
        services
            .UseApplicationMaintenanceInterface("/path/to/unix/domain/socket")
            .RegisterMaintenanceCommand("add", ConfigureAddCommand);
    })
    .Build()
    .RunAsync();

static void ConfigureAddCommand(ApplicationMaintenanceCommand command)
{
    command.Description = "Adds two integers";
    var argX = new Argument<int>("x", parse: ParseInteger, description: "The first operand")
    {
        Arity = ArgumentArity.ExactlyOne
    };
    var argY = new Argument<int>("y", parse: ParseInteger, description: "The second operand")
    {
        Arity = ArgumentArity.ExactlyOne,
    };

    command.AddArgument(argX);
    command.AddArgument(argY);
    command.SetHandler(static (x, y, console) =>
    {
        console.Out.Write((x + y).ToString());
        console.Out.Write(Environment.NewLine);
    },
    argX,
    argY,
    DefaultBindings.Console);
}
```

Registered custom commands will be automatically discovered by AMI host. [DefaultBindings](xref:DotNext.Maintenance.CommandLine.Binding.DefaultBindings) provides extra bindings available for the commands when executing by AMI host.

# Security
AMI is based on Unix Domain Sockets. It means that there is no way to have a direct connection to the application remotely. Potential attacker must have a direct access to the container or operating system to interact with the app via AMI. In most cases, it should be enough to protect the interface. However, administrator can use the following approaches to improve security:
* Configure access rights for the pseudo file at file system level
* Use authentication and authorization at AMI level

Read more [here](./auth.md) about authentication and authorization in AMI.

# Directives
AMI supports special directives aimed to control output:
* `[prnec]` - prints exit code of the command in square brackets at the beginning of the output
* `[supout]` - suppresses command output
* `[superr]` - suppresses command error output

The following example demonstrates how to add exit code to the probe output:
```sh
[prnec] probe liveness 00:00:01
```

# Example
You can find a simple application with AMI enabled [here](https://github.com/dotnet/dotNext/tree/master/src/examples/CommandLineAMI). All you need is to run it with `dotnet run` command and follow instructions printed by the app.