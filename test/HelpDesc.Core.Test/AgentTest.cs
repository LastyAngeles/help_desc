using HelpDesc.Api.Model;
using HelpDesc.Api;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Orleans.TestingHost;
using HelpDesc.Core.Extensions;
using static HelpDesc.Core.Test.Data.TestingMockData;

namespace HelpDesc.Core.Test;

[Collection(ClusterCollection.Name)]
public class AgentTest
{
    private readonly TestCluster cluster;

    public AgentTest(ClusterFixture fixture)
    {
        cluster = fixture.Cluster;
    }

    [Fact]
    public async Task BasicStatusTest()
    {
        var agentId = SolutionHelper.AgentIdFormatter("Team A", JuniorSystemName, 0);
        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var status = await agent.GetStatus();
        
        status.Should().Be(AgentStatus.Free);
    }

    [Fact]
    public async Task AgentCapacityTest()
    {
        var capacity = JuniorCapacity;
        var agentId = SolutionHelper.AgentIdFormatter("Team A", JuniorSystemName, 0);

        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var agentStatus = await agent.GetStatus();
        agentStatus.Should().Be(AgentStatus.Free);
        capacity.Should().BeGreaterThan(0);

        for (var i = 0; i < capacity; i++)
            agentStatus = await agent.AssignSession($"session.{i}");

        agentStatus.Should().Be(AgentStatus.Busy);

        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>($"session.{0}");
        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);

        await Task.Delay(SecondsBeforeSessionIsDead);

        agentStatus = await agent.GetStatus();
        agentStatus.Should().Be(AgentStatus.Free);
    }
}