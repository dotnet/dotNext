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
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using static DotNext.Threading.AsyncEvent;

namespace RaftNode
{
    public static class Program
    {
        private const string HttpProtocolOption = "http";
        private const string UdpProtocolOption = "udp";

        private static X509Certificate2 LoadCertificate()
        {
            using var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(Program), "node.pfx");
            using var ms = new MemoryStream(1024);
            rawCertificate?.CopyTo(ms);
            ms.Seek(0, SeekOrigin.Begin);
            return new X509Certificate2(ms.ToArray(), "1234");
        }

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

        private static async Task UseUdpTransport(int port, string? persistentStorage = null)
        {
            var configuration = new RaftCluster.UdpConfiguration(new IPEndPoint(IPAddress.Loopback, port))
            {
                LowerElectionTimeout = 1000,
                UpperElectionTimeout = 2000
            };
            configuration.Members.Add(new IPEndPoint(IPAddress.Loopback, 3262));
            configuration.Members.Add(new IPEndPoint(IPAddress.Loopback, 3263));
            configuration.Members.Add(new IPEndPoint(IPAddress.Loopback, 3264));
            var loggerFactory = new LoggerFactory();
            var loggerOptions = new ConsoleLoggerOptions
            {
                LogToStandardErrorThreshold = LogLevel.Warning
            };
            loggerFactory.AddProvider(new ConsoleLoggerProvider(new FakeOptionsMonitor<ConsoleLoggerOptions>(loggerOptions)));
            configuration.LoggerFactory = loggerFactory;

            using var cluster = new RaftCluster(configuration);
            cluster.LeaderChanged += ClusterConfigurator.LeaderChanged;
            await cluster.StartAsync(CancellationToken.None);
            using var handler = new CancelKeyPressHandler();
            Console.CancelKeyPress += handler.Handler;
            await handler.WaitAsync();
            Console.CancelKeyPress -= handler.Handler;
        }

        private static Task StartNode(int port, string? persistentStorage, string protocol)
        {
            switch (protocol)
            {
                case HttpProtocolOption:
                    return UseAspNetCoreHost(port, persistentStorage);
                case UdpProtocolOption:
                    return UseUdpTransport(port, persistentStorage);
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
                    Console.WriteLine("Port number is not specified");
                    break;
                case 1:
                    await StartNode(int.Parse(args[0]), null, HttpProtocolOption);
                    break;
                case 2:
                    await StartNode(int.Parse(args[0]), args[1], HttpProtocolOption);
                    break;
                case 3:
                    await StartNode(int.Parse(args[0]), args[1], args[2]);
                    break;
            }
        }
    }
}
