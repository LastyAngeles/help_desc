using System;
using System.Threading.Tasks;
using FluentAssertions;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using Orleans.TestingHost;
using Xunit;

namespace HelpDesc.Core.Test;

[Collection(ClusterCollection.Name)]
public class SessionTest
{
    private readonly TimeSpan secondsBeforeSessionIsDead = ClusterFixture.PollInterval * (ClusterFixture.MaxMissingPolls + 1);

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

        await Task.Delay(secondsBeforeSessionIsDead);

        status = await sessionGrain.GetStatus();
        status.Should().Be(SessionStatus.Dead);
    }
}