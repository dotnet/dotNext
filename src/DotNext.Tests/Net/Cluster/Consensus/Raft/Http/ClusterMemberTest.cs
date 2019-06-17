using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Xunit;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    public abstract class ClusterMemberTest : Assert
    {
        private protected static IWebHost CreateHost<TStartup>(int port, bool localhost, IDictionary<string, string> configuration)
            where TStartup : class
        {
            return new WebHostBuilder()
                .UseKestrel(options =>
                {
                    if (localhost)
                        options.ListenLocalhost(port);
                    else
                        options.ListenAnyIP(port);
                })
                .ConfigureLogging(builder => builder.AddDebug())
                .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(configuration))
                .UseStartup<TStartup>()
                .Build();
        }
    }
}
