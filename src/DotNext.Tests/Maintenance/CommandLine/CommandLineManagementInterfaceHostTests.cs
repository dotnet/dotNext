using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Hosting;

namespace DotNext.Maintenance.CommandLine
{
    using Diagnostics;

    [ExcludeFromCodeCoverage]
    public sealed class CommandLineManagementInterfaceHostTests : Test
    {
        [Fact]
        public static async Task ApplicationProbeAsync()
        {
            var unixDomainSocketPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            using var host = new HostBuilder()
                .ConfigureServices(services =>
                {
                    services
                        .UseApplicationManagementInterface(unixDomainSocketPath)
                        .UseApplicationStatusProvider<TestStatusProvider>();
                })
                .Build();

            await host.StartAsync();

            // TODO: Recreate socker according to https://github.com/dotnet/runtime/issues/71291
            var buffer = new byte[512];
            Socket socket;
            using (socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixDomainSocketPath));
                Equal("ok", await ExecuteCommandAsync(socket, "probe readiness 00:00:01", buffer));
                await socket.DisconnectAsync(true);
            }

            using (socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixDomainSocketPath));
                Equal("ok", await ExecuteCommandAsync(socket, "probe startup 00:00:01", buffer));
                await socket.DisconnectAsync(true);
            }

            using (socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(new UnixDomainSocketEndPoint(unixDomainSocketPath));
                Equal("fail", await ExecuteCommandAsync(socket, "probe liveness 00:00:01", buffer));
                await socket.DisconnectAsync(false);
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
    }
}