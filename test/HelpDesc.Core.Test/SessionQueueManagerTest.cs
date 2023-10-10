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
public class SessionQueueManagerTest
{
    private readonly TestCluster cluster;

    public SessionQueueManagerTest(ClusterFixture fixture)
    {
        cluster = fixture.Cluster;
    }

    [Fact]
    public async Task BasicCreateSessionTest()
    {
        var queueManager = cluster.GrainFactory.GetGrain<IQueueManagerGrain>(0);

        var response = await queueManager.CreateSession();

        response.ExceptionMessage.Should().BeNull();
        response.Success.Should().BeTrue();
        response.Id.Should().NotBeNull();
    }

    [Fact]
    public async Task RequestedSessionOverCapacityNotCreatedTest()
    {
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(1);
        var coreTeam = await agentManager.GetCoreTeam();
        var overflowTeam = await agentManager.GetOverflowTeam();

        var maxCapacity = coreTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency))
            .Concat(overflowTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)))
            .Sum();

        var queueManager = cluster.GrainFactory.GetGrain<IQueueManagerGrain>(2);

        for (var i = 0; i < maxCapacity; i++)
        {
            var response = await queueManager.CreateSession();
            response.ExceptionMessage.Should().BeNull();
            response.Success.Should().BeTrue();
            response.Id.Should().NotBeNull();
        }

        //from this point queue will start to populate
        var maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        for (var i = 0; i < maxQueueCapacity; i++)
        {
            var response = await queueManager.CreateSession();
            response.ExceptionMessage.Should().BeNull();
            response.Success.Should().BeTrue();
            response.Id.Should().NotBeNull();
        }

        var sessionCreation = await queueManager.CreateSession();
        sessionCreation.ExceptionMessage.Should().Contain("Queue is overloaded");
        sessionCreation.Success.Should().BeFalse();
        sessionCreation.Id.Should().BeNull();
    }

    [Fact]
    public async Task PullFromQueueAfterDisconnectTest()
    {
        var agentManager = cluster.GrainFactory.GetGrain<IAgentManagerGrain>(0);
        var coreTeam = await agentManager.GetCoreTeam();
        var overflowTeam = await agentManager.GetOverflowTeam();

        var maxAgentCapacity = coreTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency))
            .Concat(overflowTeam.Select(x => (int)Math.Floor(x.Capacity * MaxConcurrency)))
            .Sum();

        var queueManager = cluster.GrainFactory.GetGrain<IQueueManagerGrain>(0);

        for (var i = 0; i < maxAgentCapacity; i++)
            await queueManager.CreateSession();

        var maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        var firstQueuedId = (await queueManager.CreateSession()).Id;
        var firstQueuedSession = cluster.GrainFactory.GetGrain<ISessionGrain>(firstQueuedId);
        var agentId = await firstQueuedSession.GetAllocatedAgentId();
        agentId.Should().BeNull();
        
        for (var i = 1; i < maxQueueCapacity - 2; i++)
            await queueManager.CreateSession();

        var lastSession = await queueManager.CreateSession();
        lastSession.Success.Should().BeTrue();

        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>(lastSession.Id);
        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);

        await Task.Delay(SecondsBeforeSessionIsDead);

        agentId = await firstQueuedSession.GetAllocatedAgentId();
        agentId.Should().NotBeNull();
    }
}