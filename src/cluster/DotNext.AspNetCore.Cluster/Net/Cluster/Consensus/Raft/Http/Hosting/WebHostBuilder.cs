using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DotNext.Net.Cluster.Consensus.Raft.Http.Hosting
{
    internal sealed class WebHostBuilder : IWebHostBuilder
    {
        private readonly IWebHostBuilder builder;

        internal WebHostBuilder(IWebHostBuilder builder) => this.builder = builder;

        public IWebHost Build() => builder.Build();

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
