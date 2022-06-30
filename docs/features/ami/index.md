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

# AMI Hosting
AMI is supported for the application with or without Dependency Injection support. For simplicity, 

# Custom Commands
Command parsing is implemented on top of [System.CommandLine](https://docs.microsoft.com/en-us/dotnet/standard/commandline/) open-source library.

# Security


# Example
You can find a simple application with AMI enabled [here](https://github.com/dotnet/dotNext/tree/develop/src/examples/CommandLineAMI). All you need is to run it with `dotnet run` command and follow instructions printed by the app.