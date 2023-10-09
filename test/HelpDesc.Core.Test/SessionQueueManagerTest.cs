using System.Threading.Tasks;
using FluentAssertions;
using HelpDesc.Api;
using Orleans.TestingHost;
using Xunit;

namespace HelpDesc.Core.Test;

[Collection(ClusterCollection.Name)]
public class SessionQueueManagerTest
{
    public TestCluster cluster;

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
}