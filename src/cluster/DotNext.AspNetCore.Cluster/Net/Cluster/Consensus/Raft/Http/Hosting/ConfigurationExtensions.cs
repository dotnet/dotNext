using static System.Globalization.CultureInfo;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using DefaultWebHostBuilder = Microsoft.AspNetCore.Hosting.WebHostBuilder;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    public static class ConfigurationExtensions
    {
        public const string HostPortConfigurationOption = "port";

        /// <summary>
        /// Enabl
        /// </summary>
        /// <param name="services"></param>
        /// <param name="memberConfig"></param>
        /// <param name="hostBuilder"></param>
        /// <param name="appBuilder"></param>
        /// <returns></returns>
        public static IServiceCollection EnableClusterSupport(this IServiceCollection services,
            IConfiguration memberConfig, IWebHostBuilder hostBuilder = null, ApplicationBuilder appBuilder = null)
        {
            if (appBuilder != null)
                services = services.AddSingleton(appBuilder);
            if (hostBuilder is null)
            {
                hostBuilder = new DefaultWebHostBuilder();
                var port = int.Parse(memberConfig[HostPortConfigurationOption], InvariantCulture);
                hostBuilder.UseKestrel(options => options.ListenAnyIP(port));
            }

            return services.AddSingleton(new WebHostBuilder(hostBuilder))
                .AddClusterAsSingleton<RaftHostedCluster>(memberConfig);
        }
    }
}
