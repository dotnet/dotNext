using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using Messaging;
using StateMachine;

[ExcludeFromCodeCoverage]
internal sealed class Startup(IConfiguration configuration)
{
    internal const string PersistentConfigurationPath = "persistentConfigPath";

    public void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions()
            .AddSingleton(IStateMachine.CreateNoOp())
            .UsePersistentLog(new() { Location = Test.GetTempPath() })
            .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
            .AddSingleton<IInputChannel, TestMessageHandler>()
            .AddSingleton<IInputChannel, Mailbox>();

        if (configuration[PersistentConfigurationPath] is { Length: > 0 } configPath)
            services.UsePersistentConfigurationStorage(configPath);
        else
            services.UseInMemoryConfigurationStorage();
    }
}