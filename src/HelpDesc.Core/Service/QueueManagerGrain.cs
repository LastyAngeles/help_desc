using System;
using HelpDesc.Api.Model;
using HelpDesc.Api;
using Orleans;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orleans.Runtime;
using System.Linq;
using HelpDesc.Core.Extensions;
using Orleans.Streams;
using HelpDesc.Core.Service.Serialization;

namespace HelpDesc.Core.Service;

public class QueueManagerGrain : Grain, IQueueManagerGrain
{
    private readonly ILogger<QueueManagerGrain> logger;
    private readonly IPersistentState<QueueManagerInfo> queueInfo;

    public Dictionary<string, StreamSubscriptionHandle<object>> PendingSubscriptions { get; set; } = new();

    public QueueManagerGrain(ILogger<QueueManagerGrain> logger,
        [PersistentState("queueInfo", SolutionConst.HelpDescStore)]
        IPersistentState<QueueManagerInfo> queueInfo)
    {
        this.logger = logger;
        this.queueInfo = queueInfo;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);

        var maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        var sessionIds = queueInfo.State.SessionIds;

        foreach (var sessionId in sessionIds)
        {
            var agent = agentManager.AssignAgent(sessionId);
            if (agent == null)
                break;

            sessionIds = sessionIds.Remove(sessionId);
        }

        var overflowCapacityCount = maxQueueCapacity - sessionIds.Count;

        if (overflowCapacityCount > 0)
        {
            // TODO: kick the ones, who wait less! (Maxim Meshkov 2023-10-08)
            var idsToBeRemoved = new List<string>();
            for (var i = 0; i < overflowCapacityCount; i++)
            {
                idsToBeRemoved.Add(sessionIds[^1]);
                sessionIds = sessionIds.RemoveAt(sessionIds.Count - 1);
            }

            logger.LogWarning("Current team can not handle pending sessions." +
                              "Queue will be reduced to the limit of {QueueCapacity}." +
                              "Ids to be removed from queue: {RemovedIds}", maxQueueCapacity, idsToBeRemoved);
        }

        foreach (var sessionId in sessionIds)
        {
            if (!await SubscribeToSession(sessionId))
                sessionIds = sessionIds.Remove(sessionId);
        }

        queueInfo.State.SessionIds = sessionIds;
        await queueInfo.WriteStateAsync();
    }

    public async Task<SessionCreationResult> CreateSession()
    {
        var sessionId = Guid.NewGuid().ToString();

        var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);
        // TODO: cache this value properly (Maxim Meshkov 2023-10-09)
        var maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        if (queueInfo.State.SessionIds.Count + 1 > maxQueueCapacity)
        {
            const string exception =
                "Queue is overloaded, new session can not be allocated. Session creation request will be skipped.";
            logger.LogError(exception);
            return new SessionCreationResult(sessionId, false) { ExceptionMessage = exception };
        }

        if (queueInfo.State.SessionIds.Any())
            return await AddToQueue();

        var agent = agentManager.AssignAgent(sessionId);

        if (agent == null)
            return await AddToQueue();

        return new SessionCreationResult(sessionId, true);

        async Task<SessionCreationResult> AddToQueue()
        {
            if (await SubscribeToSession(sessionId))
            {
                queueInfo.State.SessionIds = queueInfo.State.SessionIds.Add(sessionId);
                await queueInfo.WriteStateAsync();
                return new SessionCreationResult(sessionId, true);
            }

            return new SessionCreationResult(sessionId, false);
        }
    }

    private async Task<bool> SubscribeToSession(string sessionId)
    {
        var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
        var status = await sessionGrain.GetStatus();
        if (status == SessionStatus.Dead)
            return false;

        var sp = this.GetStreamProvider(SolutionConst.StreamProviderName);
        var streamId = StreamId.Create(SolutionConst.SessionStreamNamespace, sessionId);
        var stream = sp.GetStream<object>(streamId);

        var subs = await stream.SubscribeAsync(async (@event, _) => { await HandleSessionEvents(sessionId, @event); });

        PendingSubscriptions[sessionId] = subs;
        return true;
    }

    public async Task<string> AllocateSinglePendingSession() => await AllocatePendingSessionInner();

    public async Task<List<string>> AllocatePendingSessions()
    {
        var ret = new List<string>();

        foreach (var _ in queueInfo.State.SessionIds)
        {
            var pendingSessionId = await AllocatePendingSessionInner(false);
            ret.Add(pendingSessionId);
        }

        await queueInfo.WriteStateAsync();

        return ret;
    }

    public async Task<string> AllocatePendingSessionInner(bool writeState = true)
    {
        if (!queueInfo.State.SessionIds.Any())
            //nothing to allocate
            return null;

        var sessionId = queueInfo.State.SessionIds.First();

        if (PendingSubscriptions.TryGetValue(sessionId, out var sessionSub))
            await sessionSub.UnsubscribeAsync();

        queueInfo.State.SessionIds = queueInfo.State.SessionIds.Remove(sessionId);

        if (writeState)
            await queueInfo.WriteStateAsync();

        return sessionId;
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
            queueInfo.State.SessionIds = queueInfo.State.SessionIds.Remove(sessionId);
            await queueInfo.WriteStateAsync();

            await subToDispose.UnsubscribeAsync();
            PendingSubscriptions.Remove(sessionId);
        }
    }
}