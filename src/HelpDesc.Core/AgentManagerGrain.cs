using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace HelpDesc.Core;

public class AgentManagerGrain : Grain, IAgentManagerGrain
{
    private readonly ILogger<AgentManagerGrain> logger;
    private readonly TeamsConfig teamsConfig;

    //priority => (agentIdx, availability)
    public Dictionary<int, List<(string, Status)>> AgentPool { get; set; }

    //priority => last allocated idx (for round robin)
    public Dictionary<int, string> PriorityRoundRobinMap { get; set; }

    public AgentManagerGrain(IOptions<TeamsConfig> teamConfigOptions, ILogger<AgentManagerGrain> logger)
    {
        this.logger = logger;
        teamsConfig = teamConfigOptions?.Value;
    }

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        base.OnActivateAsync(cancellationToken);

        if (teamsConfig == null)
        {
            logger.LogError("Error in a process of agent manager initialization - team raster is empty. Grain id: {Id}", this.GetPrimaryKeyString());
            return Task.CompletedTask;
        }

        // TODO: load from options (Maxim Meshkov 2023-10-07)

        // TODO: generate ids => (teamName + senr + idx{i})(Maxim Meshkov 2023-10-08)

        // TODO: resolve agent grain => will load its state (OR just response) and give necessary info (such as current availability) (Maxim Meshkov 2023-10-08)

        return Task.CompletedTask;
    }

    public Task<Agent> AssignAgent(string sessionId)
    {
        // TODO: implement (Maxim Meshkov 2023-10-07)
        throw new NotImplementedException();
    }
}