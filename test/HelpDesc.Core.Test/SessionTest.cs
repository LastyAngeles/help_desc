using System.Threading.Tasks;
using FluentAssertions;
using HelpDesc.Api;
using Orleans.TestingHost;
using Xunit;

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
    public async Task Basic()
    {
        var sessionGrain = cluster.GrainFactory.GetGrain<ISessionGrain>("1");

        var status = await sessionGrain.GetStatus();

        status.Should().Be(Api.Model.SessionStatus.Alive);
    }
}