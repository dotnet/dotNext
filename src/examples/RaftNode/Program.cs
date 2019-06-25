using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace RaftNode
{
    public static class Program
    {
        private static readonly Dictionary<string, string> Configuration = new Dictionary<string, string>
        {
            {"absoluteMajority", "true"},
            {"lowerElectionTimeout", "3000" },
            {"upperElectionTimeout", "4000" },
            {"members:0", "http://localhost:3262"},
            {"members:1", "http://localhost:3263"},
            //{"members:2", "http://localhost:3264"}
        };

        private static void StartNode(int port)
        {
            new WebHostBuilder()
                .UseKestrel(options => options.ListenLocalhost(port))
                .UseShutdownTimeout(TimeSpan.FromHours(1))
                //.ConfigureLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning))
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(Configuration))
                .UseStartup<Startup>()
                .Build()
                .Run();
        }

        private static void Main(string[] args)
        {
            if (args.Length > 0)
                StartNode(int.Parse(args[0]));
            else
                Console.WriteLine("Specify port number");
        }
    }
}
