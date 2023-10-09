using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core.Extensions;
using Orleans;
using Orleans.Runtime;
using Orleans.TestingHost;
using Xunit;
using static HelpDesc.Core.Test.Data.TestingMockData;

namespace HelpDesc.Core.Test;

[Collection(ClusterCollection.Name)]
public class SessionTest
{
    private readonly TestCluster cluster;

    public SessionTest(ClusterFixture fixture)
    {
        cluster = fixture.Cluster;
    }

    [Fact]
    public async Task BasicTest()
    {
        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>(Guid.NewGuid().ToString());
        var status = await sessionGrain.GetStatus();
        status.Should().Be(SessionStatus.Alive);
    }

    [Fact]
    public async Task ChangeStatusTest()
    {
        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>(Guid.NewGuid().ToString());
        var oldStatus = await sessionGrain.GetStatus();

        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);

        var newStatus = await sessionGrain.GetStatus();
        newStatus.Should().NotBe(oldStatus).And.Be(SessionStatus.Disconnected);
    }

    [Fact]
    public async Task DeadByTimerTest()
    {
        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>(Guid.NewGuid().ToString());
        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);
        
        var status = await sessionGrain.GetStatus();
        status.Should().Be(SessionStatus.Disconnected);

        await Task.Delay(SecondsBeforeSessionIsDead);

        status = await sessionGrain.GetStatus();
        status.Should().Be(SessionStatus.Dead);
    }

    [Fact]
    public async Task DisposeAfterAgentDisconnectedTest()
    {
        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>(Guid.NewGuid().ToString());
        var agentId = "agent0";
        
        var status = await sessionGrain.AllocateAgent(agentId);
        status.Should().Be(SessionStatus.Alive);

        var allocatedAgent = await sessionGrain.GetAllocatedAgentId();
        allocatedAgent.Should().Be(agentId);

        //simulate agent disposing
        var sp = cluster.Client.GetStreamProvider(SolutionConst.StreamProviderName);
        var streamId = StreamId.Create(SolutionConst.AgentStreamNamespace, agentId);
        var stream = sp.GetStream<object>(streamId);

        await stream.OnNextAsync(new AgentIsDisposing(agentId));

        //to be sure, that event actually comes into play
        await Task.Delay(1.Seconds());

        allocatedAgent = await sessionGrain.GetAllocatedAgentId();
        allocatedAgent.Should().NotBe(agentId).And.BeNull("There is no agent after disposing.");
    }
}