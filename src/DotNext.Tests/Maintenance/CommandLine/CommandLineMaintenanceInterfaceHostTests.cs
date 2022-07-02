using System.CommandLine;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Hosting;
using static System.Globalization.CultureInfo;

namespace DotNext.Maintenance.CommandLine
{
    using Authentication;
    using Authorization;
    using Binding;
    using Diagnostics;
    using Security.Principal;

    [ExcludeFromCodeCoverage]
    public sealed class CommandLineMaintenanceInterfaceHostTests : Test
    {
        [Theory]
        [InlineData("probe readiness 00:00:01", "ok")]
        [InlineData("probe startup 00:00:01", "ok")]
        [InlineData("probe liveness 00:00:01", "fail")]
        [InlineData("[prnec] probe readiness 00:00:01", "[0]ok")]
        [InlineData("[prnec] probe startup 00:00:01", "[0]ok")]
        [InlineData("[prnec] probe liveness 00:00:01", "[0]fail")]
        [InlineData("[superr] [supout] [prnec] probe readiness 00:00:01", "[0]")]
        [InlineData("[superr] [supout] [prnec] probe startup 00:00:01", "[0]")]
        [InlineData("[superr] [supout] [prnec] probe liveness 00:00:01", "[0]")]
        [InlineData("gc collect 0", "")]
        [InlineData("gc loh-compaction-mode CompactOnce", "")]
        public static async Task DefaultCommandsAsync(string request, string response)
        {
            var unixDomainSocketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .RegisterDefaultMaintenanceCommands()
                        .UseApplicationMaintenanceInterface(unixDomainSocketPath)
                        .UseApplicationStatusProvider<TestStatusProvider>();
                })
                .Build();

            await host.StartAsync();

            var buffer = new byte[512];
            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixDomainSocketPath));
                Equal(response, await ExecuteCommandAsync(socket, request, buffer));
                await socket.DisconnectAsync(true);
            }

            await host.StopAsync();
        }

        [PlatformSpecificFact("linux")]
        public static async Task UdsEndpointAuthentication()
        {
            var unixDomainSocketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .UseApplicationMaintenanceInterface(unixDomainSocketPath)
                        .UseApplicationMaintenanceInterfaceAuthentication<LinuxUdsPeerAuthenticationHandler>()
                        .RegisterMaintenanceCommand("client-pid", static command =>
                        {
                            command.SetHandler(static session =>
                            {
                                True(session.Identity.IsAuthenticated);
                                IsType<LinuxUdsPeerIdentity>(session.Identity);
                                session.ResponseWriter.Write(((LinuxUdsPeerIdentity)session.Identity).ProcessId);
                                session.ResponseWriter.Flush();
                            },
                            DefaultBindings.Session);
                        });
                })
                .Build();

            await host.StartAsync();

            var buffer = new byte[512];
            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixDomainSocketPath));
                Equal(Environment.ProcessId.ToString(InvariantCulture), await ExecuteCommandAsync(socket, "client-pid", buffer));
                await socket.DisconnectAsync(true);
            }

            await host.StopAsync();
        }

        [Theory]
        [InlineData("probe readiness 00:00:01 --login test --secret pwd", "ok")]
        [InlineData("probe startup 00:00:01 --login test --secret pwd", "ok")]
        [InlineData("probe liveness 00:00:01 --login test --secret pwd", "fail")]
        [InlineData("[superr] [supout] [prnec] add 10 20", "[77]")]
        [InlineData("[superr] [supout] [prnec] add 10 20 -login test --secret pwd", "[77]")]
        [InlineData("add 10 20 --login test2 --secret pwd", "30")]
        public static async Task PasswordAuthentication(string request, string response)
        {
            var unixDomainSocketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .UseApplicationMaintenanceInterface(unixDomainSocketPath)
                        .UseApplicationMaintenanceInterfaceAuthentication<TestPasswordAuthenticationHandler>()
                        .UseApplicationStatusProvider<TestStatusProvider>()
                        .UseApplicationMaintenanceInterfaceGlobalAuthorization(static (user, cmd, ctx, token) =>
                        {
                            True(user.Identity.IsAuthenticated);
                            return new(user.IsInRole("role1"));
                        })
                        .RegisterMaintenanceCommand("add", static cmd =>
                        {
                            var argX = new Argument<int>("x")
                            {
                                Arity = ArgumentArity.ExactlyOne,
                            };
                            cmd.AddArgument(argX);

                            var argY = new Argument<int>("y")
                            {
                                Arity = ArgumentArity.ExactlyOne,
                            };
                            cmd.AddArgument(argY);

                            cmd.SetHandler(static (x, y, session) =>
                            {
                                session.ResponseWriter.Write(x + y);
                                return session.ResponseWriter.FlushAsync();
                            },
                            argX,
                            argY,
                            DefaultBindings.Session);
                            cmd.Authorization += static (user, cmd, ctx, token) => new(user.IsInRole("role2"));
                        });
                })
                .Build();

            await host.StartAsync();

            var buffer = new byte[512];
            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixDomainSocketPath));
                Equal(response, await ExecuteCommandAsync(socket, request, buffer));
                await socket.DisconnectAsync(true);
            }

            await host.StopAsync();
        }

        private static async Task<string> ExecuteCommandAsync(Socket socket, string command, byte[] buffer)
        {
            await socket.SendAsync(Encoding.UTF8.GetBytes(command + Environment.NewLine).AsMemory(), SocketFlags.None);

            var count = await socket.ReceiveAsync(buffer.AsMemory(), SocketFlags.None);
            return Encoding.UTF8.GetString(buffer.AsSpan().Slice(0, count));
        }

        private sealed class TestStatusProvider : IApplicationStatusProvider
        {
            Task<bool> IApplicationStatusProvider.LivenessProbeAsync(CancellationToken token)
                => Task.FromResult(false);
        }

        private sealed class TestPasswordAuthenticationHandler : PasswordAuthenticationHandler
        {
            protected override ValueTask<IPrincipal> ChallengeAsync(string login, string secret, CancellationToken token)
            {
                if (string.Equals(login, "test", StringComparison.Ordinal) && string.Equals(secret, "pwd", StringComparison.Ordinal))
                    return new(new GenericPrincipal(new GenericIdentity(login), roles: new[] { "role1" }));

                if (string.Equals(login, "test2", StringComparison.Ordinal) && string.Equals(secret, "pwd", StringComparison.Ordinal))
                    return new(new GenericPrincipal(new GenericIdentity(login), roles: new[] { "role1", "role2" }));

                return new(default(IPrincipal));
            }
        }
    }
}