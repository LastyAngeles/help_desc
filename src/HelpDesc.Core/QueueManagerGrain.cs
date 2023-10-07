using HelpDesc.Api.Model;
using HelpDesc.Api;
using Orleans;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HelpDesc.Core;

public class QueueManagerGrain : Grain, IQueueManagerGrain
{
    private readonly List<string> sessionIds = new();

    // TODO: inject IAgentManager (Maxim Meshkov 2023-10-07)

    public Task<SessionCreateResult> CreateSession()
    {
        // TODO: create window (Maxim Meshkov 2023-10-07)
        return Task.FromResult(new SessionCreateResult("mainId", true));
    }
}