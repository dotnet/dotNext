using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics.CodeAnalysis;

namespace DotNext.Net.Cluster.Consensus.Raft.Http;

using IO.Log;
using Messaging;
using StateMachine;

[ExcludeFromCodeCoverage]
internal sealed class Startup(IConfiguration configuration)
{
    internal const string OptimizedLogEntryTransferKey = "optimizedLogEntryTransfer";
    internal const string PersistentConfigurationPath = "persistentConfigPath";

    public void Configure(IApplicationBuilder app)
    {
        app.UseConsensusProtocolHandler();
    }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddOptions()
            .AddSingleton(IStateMachine.CreateNoOp())
            .UsePersistentLog(new()
            {
                Location = Test.GetTempPath(),
                OptimizedLogEntryTransfer = configuration.GetValue(OptimizedLogEntryTransferKey, true),
            })
            .AddSingleton<IHttpMessageHandlerFactory, RaftClientHandlerFactory>()
            .AddSingleton<IInputChannel, TestMessageHandler>()
            .AddSingleton<IInputChannel, Mailbox>();

        if (configuration[PersistentConfigurationPath] is { Length: > 0 } configPath)
            services.UsePersistentConfigurationStorage(configPath);
        else
            services.UseInMemoryConfigurationStorage();
    }
}

file static class RegistrationHelpers
{
    extension(IServiceCollection services)
    {
        public IServiceCollection UsePersistentLog(TestWalOptions options)
            => options.OptimizedLogEntryTransfer
                ? RaftClusterConfiguration.UsePersistentLog(services, options)
                : services.UseWriteAheadLog(options.CreateWriteAheadLog);

        private IServiceCollection UseWriteAheadLog(Func<IServiceProvider, IPersistentState> factory)
        {
            Func<IServiceProvider, IPersistentState> engineCast = ServiceProviderServiceExtensions.GetRequiredService<IPersistentState>;

            return services.AddSingleton(factory)
                .AddSingleton<IAuditTrail<IRaftLogEntry>>(engineCast);
        }
    }

    private static IPersistentState CreateWriteAheadLog(this TestWalOptions options, IServiceProvider provider)
        => new TestWriteAheadLog(options, provider.GetRequiredService<IStateMachine>());
}

file sealed class TestWalOptions : WriteAheadLog.Options
{
    public bool OptimizedLogEntryTransfer { get; init; }
}

file sealed class TestWriteAheadLog(TestWalOptions options, IStateMachine stateMachine) : WriteAheadLog(options, stateMachine), IAuditTrail
{
    bool IAuditTrail.IsLogEntryLengthAlwaysPresented { get; } = options.OptimizedLogEntryTransfer;
}