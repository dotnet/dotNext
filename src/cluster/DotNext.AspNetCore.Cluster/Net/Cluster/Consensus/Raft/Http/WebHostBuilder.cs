using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft.Http
{
    internal sealed class WebHostBuilder : IWebHostBuilder
    {
        private sealed class RequestDelegateConfigurer : StartupBase
        {
            private readonly RequestDelegate requestHandler;

            internal RequestDelegateConfigurer(RequestDelegate requestHandler)
                => this.requestHandler = requestHandler;

            IServiceProvider IStartup.ConfigureServices(IServiceCollection services) => services.

            public void Configure(IApplicationBuilder app)
            {
                throw new NotImplementedException();
            }
        }

        private readonly IWebHostBuilder builder;

        internal WebHostBuilder(IWebHostBuilder builder) => this.builder = builder;

        public IWebHost Build() => builder.Build();

        internal void AddProtocolEndpoint(RequestDelegate requestHandler)
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStartup>()
            });
        }

        IWebHostBuilder IWebHostBuilder.ConfigureAppConfiguration(
            Action<WebHostBuilderContext, IConfigurationBuilder> configureDelegate)
            => builder.ConfigureAppConfiguration(configureDelegate);

        IWebHostBuilder IWebHostBuilder.ConfigureServices(Action<IServiceCollection> configureServices)
            => builder.ConfigureServices(configureServices);

        IWebHostBuilder IWebHostBuilder.ConfigureServices(
            Action<WebHostBuilderContext, IServiceCollection> configureServices)
            => builder.ConfigureServices(configureServices);

        string IWebHostBuilder.GetSetting(string key) => builder.GetSetting(key);

        IWebHostBuilder IWebHostBuilder.UseSetting(string key, string value) => builder.UseSetting(key, value);
    }
}
