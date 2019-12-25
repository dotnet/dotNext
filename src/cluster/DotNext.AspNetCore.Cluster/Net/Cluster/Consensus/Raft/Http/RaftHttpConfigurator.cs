using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;
using System.Threading.Tasks;
using Debug = System.Diagnostics.Debug;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    using DistributedServices;
    using Messaging;
    using Replication;
    using static Reflection.CustomAttribute;
    using DistributedServiceProviderAttribute = Runtime.CompilerServices.DistributedServiceProviderAttribute;
    internal static class RaftHttpConfigurator
    {
        private static IDistributedApplicationEnvironment GetDistributedServices(IServiceProvider services)
        {
            var cluster = services.GetRequiredService<RaftHttpCluster>();
            return cluster.IsDistributedServicesSupported ? cluster : throw new NotSupportedException(ExceptionMessages.DistributedServicesAreUnavailable);
        }

        private static object GetDistributedService(this Func<IDistributedApplicationEnvironment, object> propertyGetter, IServiceProvider services)
            => propertyGetter(services.GetRequiredService<IDistributedApplicationEnvironment>());

        private static IServiceCollection RegisterDistributedServices(this IServiceCollection services)
        {
            const BindingFlags propertyFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;
            foreach(var serviceProperty in typeof(IDistributedApplicationEnvironment).GetProperties(propertyFlags))
                if(serviceProperty.IsDefined<DistributedServiceProviderAttribute>())
                {
                    var getter = serviceProperty.GetMethod?.CreateDelegate<Func<IDistributedApplicationEnvironment, object>>();
                    Debug.Assert(getter != null);
                    services.AddSingleton(serviceProperty.PropertyType, getter.GetDistributedService);
                }
            return services;
        }

        internal static IServiceCollection AddClusterAsSingleton<TCluster, TConfig>(this IServiceCollection services, IConfiguration memberConfig)
            where TCluster : RaftHttpCluster
            where TConfig : RaftClusterMemberConfiguration, new()
        {
            Func<IServiceProvider, RaftHttpCluster> clusterNodeCast =
                ServiceProviderServiceExtensions.GetRequiredService<TCluster>;
            return services.Configure<TConfig>(memberConfig)
                .Configure<RaftClusterMemberConfiguration>(memberConfig)
                .AddSingleton<TCluster>()
                .AddSingleton(clusterNodeCast)
                .AddSingleton(GetDistributedServices)
                .RegisterDistributedServices()
                .AddSingleton<IHostedService>(clusterNodeCast)
                .AddSingleton<ICluster>(clusterNodeCast)
                .AddSingleton<IRaftCluster>(clusterNodeCast)
                .AddSingleton<IMessageBus>(clusterNodeCast)
                .AddSingleton<IReplicationCluster>(clusterNodeCast)
                .AddSingleton<IReplicationCluster<IRaftLogEntry>>(clusterNodeCast)
                .AddSingleton<IExpandableCluster>(clusterNodeCast);
        }

        internal static Task WriteExceptionContent(HttpContext context)
        {
            var feature = context.Features.Get<IExceptionHandlerFeature>();
            return feature is null ? Task.CompletedTask : context.Response.WriteAsync(feature.Error.ToString());
        }
    }
}
