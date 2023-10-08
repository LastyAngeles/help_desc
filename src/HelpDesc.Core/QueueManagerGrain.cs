using System;
using HelpDesc.Api.Model;
using HelpDesc.Api;
using Orleans;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace HelpDesc.Core;

public class QueueManagerGrain : Grain, IQueueManagerGrain
{
    private readonly ILogger<QueueManagerGrain> logger;
    private List<string> sessionIds;

    private double maxQueueCapacity;

    public QueueManagerGrain(ILogger<QueueManagerGrain> logger)
    {
        this.logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        // TODO: load all persisted session ids (Maxim Meshkov 2023-10-08)
        sessionIds = new List<string>();

        var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);

        maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        foreach (var sessionId in sessionIds)
        {
            var agent = agentManager.AssignAgent(sessionId);
            if (agent == null)
                break;
            sessionIds.Remove(sessionId);
        }

        var overflowCapacityCount = maxQueueCapacity - sessionIds.Count;

        if (overflowCapacityCount > 0)
        {
            // TODO: kick the ones, who wait less! (Maxim Meshkov 2023-10-08)
            var idsToBeRemoved = new List<string>();
            for (var i = 0; i < overflowCapacityCount; i++)
            {
                idsToBeRemoved.Add(sessionIds[^1]);
                sessionIds.RemoveAt(sessionIds.Count - 1);
            }

            logger.LogWarning("Current team can not handle pending sessions." +
                              "Queue will be reduced to the limit of {QueueCapacity}." +
                              "Ids to be removed from queue: {RemovedIds}", maxQueueCapacity, idsToBeRemoved);
        }

        await base.OnActivateAsync(cancellationToken);
    }

    public async Task<SessionCreationResult> CreateSession()
    {
        var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);
        var sessionId = Guid.NewGuid().ToString();
        var agent = await agentManager.AssignAgent(sessionId);

        if (agent == null)
        {
            // TODO: enqueue on w8 list (Maxim Meshkov 2023-10-08)
            // TODO: validate if there any space in queue (Maxim Meshkov 2023-10-08)
        }

        return new SessionCreationResult(sessionId, true);
    }
}