using HelpDesc.Api.Model;
using HelpDesc.Api;
using Orleans;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HelpDesc.Core;

public class QueueManagerGrain : Grain, IQueueManagerGrain
{
    private readonly List<string> sessionIds = new();

    // TODO: inject IAgentManager (Maxim Meshkov 2023-10-07)

    public override Task OnActivateAsync(CancellationToken cancellationToken)
    {
        base.OnActivateAsync(cancellationToken);

        // TODO: load all session ids (Maxim Meshkov 2023-10-08)

        // TODO: ask IAgentManager max capacity of queue (Maxim Meshkov 2023-10-08)

        // TODO: ask IAgentManager to take some of the pending sessions until its full (Maxim Meshkov 2023-10-08)

        // TODO: validate that the rest of the queue is <= maxCapacity (team before was 10 people fully loaded / now its only one)(Maxim Meshkov 2023-10-08)
        // TODO: kick the ones who wait less! (Error case) (Maxim Meshkov 2023-10-08)
        return Task.CompletedTask;
    }

    public Task<SessionCreateResult> CreateSession()
    {
        // TODO: create window (Maxim Meshkov 2023-10-07)
        return Task.FromResult(new SessionCreateResult("mainId", true));
    }
}