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
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(Guid.NewGuid().ToString());
        var sessionId = Guid.NewGuid().ToString();

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
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(Guid.NewGuid().ToString());
        var sessionId = Guid.NewGuid().ToString();

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

    [Fact]
    public async Task SessionRequestNotAllocatedAfterOverloadTest()
    {
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(Guid.NewGuid().ToString());
        var sessionId = Guid.NewGuid().ToString();

        var coreTeam = await agentManager.GetCoreTeam();
        var overflowTeam = await agentManager.GetOverflowTeam();

        var maxCoreCapacity = coreTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)).Sum();
        var maxOverflowCapacity = overflowTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)).Sum();

        for (var i = 0; i < maxCoreCapacity; i++)
            await agentManager.AssignAgent($"{sessionId}.core.{i}");

        for (var i = 0; i < maxOverflowCapacity; i++)
            await agentManager.AssignAgent($"{sessionId}.overflow.{i}");

        coreTeam = await agentManager.GetCoreTeam();
        coreTeam.Select(x => x.Availability == AgentStatus.Busy).Count().Should().Be(coreTeam.Count);

        overflowTeam = await agentManager.GetOverflowTeam();
        overflowTeam.Select(x => x.Availability == AgentStatus.Busy).Count().Should().Be(overflowTeam.Count);

        var agent = await agentManager.AssignAgent($"{sessionId}.notAllocated.{0}");
        agent.Should().BeNull();

        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>($"{sessionId}.notAllocated.{0}");
        var agentId = await sessionGrain.GetAllocatedAgentId();

        agentId.Should().BeNull();
    }

    [Fact]
    public async Task AllocateSessionAfterAgentBecameFreeTest()
    {
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(Guid.NewGuid().ToString());
        var sessionId = Guid.NewGuid().ToString();

        var coreTeam = await agentManager.GetCoreTeam();
        var overflowTeam = await agentManager.GetOverflowTeam();

        var maxCoreCapacity = coreTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)).Sum();
        var maxOverflowCapacity = overflowTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)).Sum();

        for (var i = 0; i < maxCoreCapacity; i++)
            await agentManager.AssignAgent($"{sessionId}.core.{i}");

        for (var i = 0; i < maxOverflowCapacity; i++)
            await agentManager.AssignAgent($"{sessionId}.overflow.{i}");

        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>($"{sessionId}.core.{0}");
        var agentId = await sessionGrain.GetAllocatedAgentId();

        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);
        (await agent.GetStatus()).Should().Be(AgentStatus.Busy);

        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);

        await Task.Delay(SecondsBeforeSessionIsDead);

        (await agent.GetStatus()).Should().Be(AgentStatus.Free);

        var newlyAssignedAgent = await agentManager.AssignAgent($"{sessionId}.freshSession.{0}");

        newlyAssignedAgent.Id.Should().Be(agentId);
        newlyAssignedAgent.Availability.Should().Be(AgentStatus.Busy);
    }
}