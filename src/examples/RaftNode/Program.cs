using DotNext.Net.Cluster.Consensus.Raft;
using DotNext.Net.Cluster.Consensus.Raft.Http.Embedding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using SslOptions = DotNext.Net.Security.SslOptions;
using static DotNext.Threading.AsyncEvent;

namespace RaftNode
{
    public static class Program
    {
        private static Task UseAspNetCoreHost(int port, string? persistentStorage = null)
        {
            var configuration = new Dictionary<string, string>
            {
                {"partitioning", "false"},
                {"lowerElectionTimeout", "150" },
                {"upperElectionTimeout", "300" },
                {"members:0", "https://localhost:3262"},
                {"members:1", "https://localhost:3263"},
                {"members:2", "https://localhost:3264"},
                {"requestJournal:memoryLimit", "5" },
                {"requestJournal:expiration", "00:01:00" }
            };
            if (!string.IsNullOrEmpty(persistentStorage))
                configuration[SimplePersistentState.LogLocation] = persistentStorage;
            return new HostBuilder().ConfigureWebHost(webHost =>
            {
                webHost.UseKestrel(options =>
                {
                    options.ListenLocalhost(port, listener => listener.UseHttps(LoadCertificate()));
                })
                .UseStartup<Startup>();
            })
            .ConfigureLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Error))
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
            .JoinCluster()
            .Build()
            .RunAsync();
        }

        private static async Task UseConfiguration(RaftCluster.NodeConfiguration config, string? persistentStorage)
        {
            config.Members.Add(new IPEndPoint(IPAddress.Loopback, 3262));
            config.Members.Add(new IPEndPoint(IPAddress.Loopback, 3263));
            config.Members.Add(new IPEndPoint(IPAddress.Loopback, 3264));
            var loggerFactory = new LoggerFactory();
            var loggerOptions = new ConsoleLoggerOptions
            {
                LogToStandardErrorThreshold = LogLevel.Warning
            };
            loggerFactory.AddProvider(new ConsoleLoggerProvider(new FakeOptionsMonitor<ConsoleLoggerOptions>(loggerOptions)));
            config.LoggerFactory = loggerFactory;

            using var cluster = new RaftCluster(config);
            cluster.LeaderChanged += ClusterConfigurator.LeaderChanged;
            var modifier = default(DataModifier?);
            if (!string.IsNullOrEmpty(persistentStorage))
            {
                var state = new SimplePersistentState(persistentStorage);
                cluster.AuditTrail = state;
                modifier = new DataModifier(cluster, state);
            }
            await cluster.StartAsync(CancellationToken.None);
            await (modifier?.StartAsync(CancellationToken.None) ?? Task.CompletedTask);
            using var handler = new CancelKeyPressHandler();
            Console.CancelKeyPress += handler.Handler;
            await handler.WaitAsync();
            Console.CancelKeyPress -= handler.Handler;
            await (modifier?.StopAsync(CancellationToken.None) ?? Task.CompletedTask);
            await cluster.StopAsync(CancellationToken.None);
        }

        private static Task UseUdpTransport(int port, string? persistentStorage)
        {
            var configuration = new RaftCluster.UdpConfiguration(new IPEndPoint(IPAddress.Loopback, port))
            {
                LowerElectionTimeout = 150,
                UpperElectionTimeout = 300,
                DatagramSize = 1024
            };
            return UseConfiguration(configuration, persistentStorage);
        }

        private static Task UseTcpTransport(int port, string? persistentStorage, bool useSsl)
        {
            var configuration = new RaftCluster.TcpConfiguration(new IPEndPoint(IPAddress.Loopback, port))
            {
                LowerElectionTimeout = 150,
                UpperElectionTimeout = 300,
                TransmissionBlockSize = 4096,
                SslOptions = useSsl ? CreateSslOptions() : null
            };

            return UseConfiguration(configuration, persistentStorage);

            static SslOptions CreateSslOptions()
            {
                var options = new SslOptions();
                options.ServerOptions.ServerCertificate = LoadCertificate();
                options.ClientOptions.TargetHost = "localhost";
                options.ClientOptions.RemoteCertificateValidationCallback = AllowAnyCert;
                return options;
            }
        }

        private static Task StartNode(string protocol, int port, string? persistentStorage = null)
        {
            switch (protocol.ToLowerInvariant())
            {
                case "http":
                case "https":
                    return UseAspNetCoreHost(port, persistentStorage);
                case "udp":
                    return UseUdpTransport(port, persistentStorage);
                case "tcp":
                    return UseTcpTransport(port, persistentStorage, false);
                case "tcp+ssl":
                    return UseTcpTransport(port, persistentStorage, true);
                default:
                    Console.Error.WriteLine("Unsupported protocol type");
                    Environment.ExitCode = 1;
                    return Task.CompletedTask;
            }
        }

        private static async Task Main(string[] args)
        {
            switch (args.LongLength)
            {
                case 0:
                case 1:
                    Console.WriteLine("Port number and protocol are not specified");
                    break;
                case 2:
                    await StartNode(args[0], int.Parse(args[1]));
                    break;
                case 3:
                    await StartNode(args[0], int.Parse(args[1]), args[2]);
                    break;
            }
        }

        private static X509Certificate2 LoadCertificate()
        {
            using var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(Program), "node.pfx");
            using var ms = new MemoryStream(1024);
            rawCertificate?.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return new X509Certificate2(ms.ToArray(), "1234");
        }

        private static bool AllowAnyCert(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
            => true;
    }
}
