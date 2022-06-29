using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace DotNext.Maintenance.CommandLine
{
    using Authentication;
    using Diagnostics;

    [ExcludeFromCodeCoverage]
    public sealed class CommandLineMaintenanceInterfaceHostTests : Test
    {
        [Theory]
        [InlineData("probe readiness 00:00:01", "ok")]
        [InlineData("probe startup 00:00:01", "ok")]
        [InlineData("probe liveness 00:00:01", "fail")]
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

        [Theory]
        [InlineData("probe readiness 00:00:01 --login test --secret pwd", "ok")]
        [InlineData("probe startup 00:00:01 --login test --secret pwd", "ok")]
        [InlineData("probe liveness 00:00:01 --login test --secret pwd", "fail")]
        public static async Task PasswordAuthentication(string request, string response)
        {
            var unixDomainSocketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .UseApplicationMaintenanceInterface(unixDomainSocketPath)
                        .UseApplicationMaintenanceInterfaceAuthentication<TestPasswordAuthenticationHandler>()
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
                => new(string.Equals(login, "test", StringComparison.Ordinal) && string.Equals(secret, "pwd", StringComparison.Ordinal) ? new GenericPrincipal(new GenericIdentity(login), roles: null) : null);
        }
    }
}