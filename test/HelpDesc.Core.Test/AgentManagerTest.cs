using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using Orleans.TestingHost;
using Xunit;
using static HelpDesc.Core.Test.Data.TestingMockData;

namespace HelpDesc.Core.Test;

[Collection(ClusterCollection.Name)]
public class AgentManagerTest
{
    private readonly TestCluster cluster;

    public AgentManagerTest(ClusterFixture fixture)
    {
        cluster = fixture.Cluster;
    }

    [Fact]
    public async Task BasicSessionAllocationTest()
    {
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(0);
        const string sessionId = "sessionId";

        var agent = await agentManager.AssignAgent(sessionId);
        agent.Should().NotBeNull();
        agent.Availability.Should().Be(AgentStatus.Free);

        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>(sessionId);
        var allocatedAgentId = await sessionGrain.GetAllocatedAgentId();

        allocatedAgentId.Should().Be(agent.Id);
    }

    [Fact]
    public async Task TeamOverloadTest()
    {
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(0);
        const string sessionId = "sessionId";

        var coreTeam = await agentManager.GetCoreTeam();
        var overflowTeam = await agentManager.GetOverflowTeam();

        var maxCoreCapacity = coreTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)).Sum();

        for (var i = 0; i < maxCoreCapacity; i++)
            await agentManager.AssignAgent($"{sessionId}.core.{i}");

        coreTeam = await agentManager.GetCoreTeam();
        coreTeam.Select(x => x.Availability == AgentStatus.Busy).Count().Should().Be(coreTeam.Count);

        var overflowAgent = await agentManager.AssignAgent($"{sessionId}.overflow.{100}");

        overflowTeam.Should().Contain(overflowAgent);
    }
}