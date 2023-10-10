using System;
using Orleans.TestingHost;
using Orleans.Hosting;
using HelpDesc.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using HelpDesc.Core.Service;
using Microsoft.Extensions.Configuration;
using static HelpDesc.Core.Test.Data.TestingMockData;

namespace HelpDesc.Core.Test;

public class ClusterFixture : IDisposable
{
    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
        builder.AddClientBuilderConfigurator<TestClientConfigurations>();
        builder.AddSiloBuilderConfigurator<TestSiloConfigurations>();
        Cluster = builder.Build();
        Cluster.Deploy();
    }

    public void Dispose()
    {
        Cluster.StopAllSilos();
    }

    public TestCluster Cluster { get; }
}

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        siloBuilder
            .AddMemoryStreams(SolutionConst.StreamProviderName)
            .AddMemoryGrainStorage("PubSubStore")
            .AddMemoryGrainStorage(SolutionConst.HelpDescStore)
            .UseInMemoryReminderService();

        siloBuilder.ConfigureServices(services =>
        {
            services.AddSingleton<TestTimeProvider>();
            services.AddSingleton<ITimeProvider, TestTimeProvider>(sp => sp.GetRequiredService<TestTimeProvider>());
            services.Configure<Intervals>(conf =>
            {
                conf.MaximumQueueCapacityMultiplier = MaxQueueCapacityMultiplier;
                conf.MaximumConcurrency = MaxConcurrency;
                conf.MaxMissingPolls = MaxMissingPolls;
                conf.SessionPollInterval = PollInterval;
            });

            services.Configure<TeamsConfig>(teamsConfig =>
            {
                teamsConfig.CoreTeams = CoreTeams;
                teamsConfig.OverflowTeam = OverflowTeam;
                teamsConfig.SeniorityDescriptions = SeniorityDescriptions;
            });
        });
    }
}

public class TestClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        clientBuilder.AddMemoryStreams(SolutionConst.StreamProviderName);
    }
}