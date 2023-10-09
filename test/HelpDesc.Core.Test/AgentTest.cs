using HelpDesc.Api.Model;
using HelpDesc.Api;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;
using Orleans.TestingHost;
using HelpDesc.Core.Extensions;
using HelpDesc.Core.Test.Data;

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
        var agentId = SolutionHelper.AgentIdFormatter("Team A", TestingMockData.JuniorSystemName, 0);
        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>(agentId);

        var status = await agent.GetStatus();
        
        status.Should().Be(AgentStatus.Free);
    }

    [Fact]
    public async Task AssignSessionTest()
    {
        var agent = cluster.GrainFactory.GetGrain<IAgentGrain>("1");

        var status = await agent.AssignSession("123");

        status.Should().Be(AgentStatus.Free);
    }
}