using System;
using HelpDesc.Api.Model;
using HelpDesc.Api;
using Orleans;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using System.Collections.Immutable;
using System.Linq;
using HelpDesc.Core.Extensions;
using Orleans.Streams;

namespace HelpDesc.Core;

public class QueueManagerGrain : Grain, IQueueManagerGrain
{
    private readonly ILogger<QueueManagerGrain> logger;
    private readonly IPersistentState<ImmutableList<string>> sessionsInQueue;

    public Dictionary<string, StreamSubscriptionHandle<object>> PendingSubscriptions { get; set; } = new();

    // TODO: field can be outdated! (Maxim Meshkov 2023-10-08)
    private double maxQueueCapacity;

    public QueueManagerGrain(ILogger<QueueManagerGrain> logger,
        [PersistentState("sessionsInQueue", "helpDescStore")]
        IPersistentState<ImmutableList<string>> sessionsInQueue)
    {
        this.logger = logger;
        this.sessionsInQueue = sessionsInQueue;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);

        maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        foreach (var sessionId in sessionsInQueue.State)
        {
            var agent = agentManager.AssignAgent(sessionId);
            if (agent == null)
                break;
            sessionsInQueue.State = sessionsInQueue.State.Remove(sessionId);
        }

        var overflowCapacityCount = maxQueueCapacity - sessionsInQueue.State.Count;

        if (overflowCapacityCount > 0)
        {
            // TODO: kick the ones, who wait less! (Maxim Meshkov 2023-10-08)
            var idsToBeRemoved = new List<string>();
            for (var i = 0; i < overflowCapacityCount; i++)
            {
                idsToBeRemoved.Add(sessionsInQueue.State[^1]);
                sessionsInQueue.State = sessionsInQueue.State.RemoveAt(sessionsInQueue.State.Count - 1);
            }

            logger.LogWarning("Current team can not handle pending sessions." +
                              "Queue will be reduced to the limit of {QueueCapacity}." +
                              "Ids to be removed from queue: {RemovedIds}", maxQueueCapacity, idsToBeRemoved);
        }
    }

    public async Task<SessionCreationResult> CreateSession()
    {
        var sessionId = Guid.NewGuid().ToString();

        if (sessionsInQueue.State.Count + 1 > maxQueueCapacity)
        {
            const string exception = "Queue is overloaded, new session can not be allocated. Session creation request will be skipped.";
            logger.LogError(exception);
            return new SessionCreationResult(sessionId, false) { ExceptionMessage = exception };
        }

        var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);

        if (sessionsInQueue.State.Any())
        {
            sessionsInQueue.State = sessionsInQueue.State.Insert(0, sessionId);
            await sessionsInQueue.WriteStateAsync();
            return new SessionCreationResult(sessionId, true);
        }

        var agent = agentManager.AssignAgent(sessionId);

        if (agent == null)
        {
            //get the grain in order to start timer
            GrainFactory.GetGrain<ISessionGrain>(sessionId);
            // TODO: ping grain? (Maxim Meshkov 2023-10-08)

            var sp = this.GetStreamProvider(StreamingConst.SessionStreamName);
            var streamId = StreamId.Create(StreamingConst.SessionStreamNamespace, sessionId);
            var stream = sp.GetStream<object>(streamId);

            var subs = await stream.SubscribeAsync(async (@event, _) =>
            {
                await HandleSessionEvents(sessionId, @event);
            });

            sessionsInQueue.State = sessionsInQueue.State.Add(sessionId);
            PendingSubscriptions[sessionId] = subs;
            await sessionsInQueue.WriteStateAsync();
        }

        return new SessionCreationResult(sessionId, true);
    }

    private async Task HandleSessionEvents(string sessionId, object @event)
    {
        if (@event is SessionDeadEvent _)
        {
            await RemoveSession(sessionId);
        }
    }

    private async Task RemoveSession(string sessionId)
    {
        if (PendingSubscriptions.TryGetValue(sessionId, out var subToDispose))
        {
            sessionsInQueue.State = sessionsInQueue.State.Remove(sessionId);
            await sessionsInQueue.WriteStateAsync();

            await subToDispose.UnsubscribeAsync();
            PendingSubscriptions.Remove(sessionId);
        }
    }
}