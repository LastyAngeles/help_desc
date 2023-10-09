using System;
using Orleans.TestingHost;
using Orleans.Hosting;
using HelpDesc.Core.Extensions;
using Microsoft.Extensions.DependencyInjection;
using HelpDesc.Core.Service;
using static HelpDesc.Core.Test.Data.TestingMockData;

namespace HelpDesc.Core.Test;

public class ClusterFixture : IDisposable
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    public static readonly int MaxMissingPolls = 3;

    public ClusterFixture()
    {
        var builder = new TestClusterBuilder();
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
            services.Configure<TeamsConfig>(teamsConfig => {
                teamsConfig.CoreTeams = CoreTeams;
                teamsConfig.OverflowTeam = OverflowTeam;
                teamsConfig.MaximumQueueCapacityMultiplier = MaxQueueCapacityMultiplier;
                teamsConfig.SeniorityDescriptions = SeniorityDescriptions;
                teamsConfig.MaxMissingPolls = MaxMissingPolls;
                teamsConfig.SessionPollInterval = PollInterval;
            });
        });
    }
}