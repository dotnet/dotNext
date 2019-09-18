using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;

namespace RaftNode
{
    public static class Program
    {
        private static X509Certificate2 LoadCertificate()
        {
            using (var rawCertificate = Assembly.GetCallingAssembly().GetManifestResourceStream(typeof(Program), "node.pfx"))
            using (var ms = new MemoryStream(1024))
            {
                rawCertificate.CopyTo(ms);
                ms.Seek(0, SeekOrigin.Begin);
                return new X509Certificate2(ms.ToArray(), "1234");
            }
        }

        private static void StartNode(int port, string persistentStorage = null)
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
            new WebHostBuilder()
                .UseKestrel(options =>
                {
                    options.ListenLocalhost(port, listener => listener.UseHttps(LoadCertificate()));
                })
                .UseShutdownTimeout(TimeSpan.FromHours(1))
                .ConfigureLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Error))
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .UseStartup<Startup>()
                .Build()
                .Run();
        }

        private static void Main(string[] args)
        {
            switch (args.LongLength)
            {
                case 0:
                    Console.WriteLine("Port number is not specified");
                    break;
                case 1:
                    StartNode(int.Parse(args[0]));
                    break;
                case 2:
                    StartNode(int.Parse(args[0]), args[1]);
                    break;
            }
        }
    }
}
