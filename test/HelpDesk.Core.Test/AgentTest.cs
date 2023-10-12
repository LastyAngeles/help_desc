using HelpDesk.Api.Model;
using HelpDesk.Api;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Orleans.TestingHost;
using HelpDesk.Core.Extensions;
using static HelpDesk.Core.Test.Data.TestingMockData;
using System;

namespace HelpDesk.Core.Test;

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
        var primaryGrainId = Guid.NewGuid().ToString();

        var agentId = SolutionHelper.AgentIdFormatter(primaryGrainId, "Team A", JuniorSystemName, 0);
        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var workLoad = await agent.GetCurrentWorkload();
        workLoad.Should().Be(0);

        var status = await agent.GetStatus();
        status.Should().Be(AgentStatus.Free);
    }

    [Fact]
    public async Task AgentAllocationTest()
    {
        var primaryGrainId = Guid.NewGuid().ToString();
        var capacity = JuniorCapacity - 1;
        var agentId = SolutionHelper.AgentIdFormatter(primaryGrainId, "Team A", JuniorSystemName, 0);

        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var agentStatus = await agent.GetStatus();
        agentStatus.Should().Be(AgentStatus.Free);
        capacity.Should().BeGreaterThan(0);

        for (var i = 0; i < capacity; i++)
        {
            agentStatus = await agent.AssignSession($"{nameof(AgentAllocationTest)}.session.{i}");
            agentStatus.Should().Be(AgentStatus.Free);
            var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>($"{nameof(AgentAllocationTest)}.session.{i}");
            var allocatedAgentId = await sessionGrain.GetAllocatedAgentId();
            allocatedAgentId.Should().Be(agentId);
        }
    }

    [Fact]
    public async Task AgentCapacityTest()
    {
        var primaryGrainId = Guid.NewGuid().ToString();
        var capacity = JuniorCapacity;
        var agentId = SolutionHelper.AgentIdFormatter(primaryGrainId, "Team A", JuniorSystemName, 0);

        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var agentStatus = await agent.GetStatus();
        agentStatus.Should().Be(AgentStatus.Free);
        capacity.Should().BeGreaterThan(0);

        for (var i = 0; i < capacity; i++)
            agentStatus = await agent.AssignSession($"{nameof(AgentCapacityTest)}.session.{i}");

        agentStatus.Should().Be(AgentStatus.Busy);

        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>($"{nameof(AgentCapacityTest)}.session.{0}");
        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);
        var agentIdBeforeDispose = await sessionGrain.GetAllocatedAgentId();
        agentIdBeforeDispose.Should().Be(agentId);

        await Task.Delay(SecondsBeforeSessionIsDead);

        var agentIdAfterDispose = await sessionGrain.GetAllocatedAgentId();
        agentIdAfterDispose.Should().NotBe(agentIdBeforeDispose).And.BeNull();

        agentStatus = await agent.GetStatus();
        agentStatus.Should().Be(AgentStatus.Free);
    }

    [Fact]
    public async Task AgentOverloadCapacityTest()
    {
        var primaryGrainId = Guid.NewGuid().ToString();
        var capacity = MiddleCapacity;
        var agentId = SolutionHelper.AgentIdFormatter(primaryGrainId, "Team A", MiddleSystemName, 0);

        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var agentStatus = await agent.GetStatus();
        agentStatus.Should().Be(AgentStatus.Free);
        capacity.Should().BeGreaterThan(0);

        //attempt to allocate session above capacity
        for (var i = 0; i < capacity + 1; i++)
            agentStatus = await agent.AssignSession($"{nameof(AgentOverloadCapacityTest)}.session.{i}");

        agentStatus.Should().Be(AgentStatus.Overloaded);

        agentStatus = await agent.GetStatus();
        //overload is not a proper status, so it should not hide proper one
        agentStatus.Should().Be(AgentStatus.Busy);

        //create session which should not be allocated
        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>($"{nameof(AgentCapacityTest)}.session.{capacity}");
        var allocatedAgentId = await sessionGrain.GetAllocatedAgentId();
        allocatedAgentId.Should().BeNull("No one allocate this session, because agent capacity was overloaded.");
        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);

        await Task.Delay(SecondsBeforeSessionIsDead);

        agentStatus = await agent.GetStatus();
        //closing not allocated session should not take effect
        agentStatus.Should().Be(AgentStatus.Busy);
    }
}