using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core.Extensions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace HelpDesc.Core;

public class AgentGrain : Grain, IAgentGrain
{
    private readonly IOptions<TeamsConfig> teamConfig;
    private readonly IPersistentState<ImmutableList<string>> processingSessions;

    //sessionId => subscription
    public Dictionary<string, StreamSubscriptionHandle<object>> RunningSubscriptions { get; set; } = new();

    private Status currentStatus;
    private int maxCapacity;

    public AgentGrain(IOptions<TeamsConfig> teamConfig,
        [PersistentState("agentsSessions", "helpDescStore")]
        IPersistentState<ImmutableList<string>> processingSessions)
    {
        this.teamConfig = teamConfig;
        this.processingSessions = processingSessions;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var sessionIds = processingSessions.State;

        SetMaxCapacity();

        var updateIsRequired = false;

        foreach (var sessionId in sessionIds)
        {
            var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
            var sessionStatus = sessionGrain.GetStatus();
            if (sessionStatus == SessionStatus.Dead)
            {
                updateIsRequired = true;
                processingSessions.State = sessionIds.Remove(sessionId);
                continue;
            }

            currentStatus = await AssignSession(sessionId);
            // TODO: what if sessionIds.Count > maxCapacity? (Maxim Meshkov 2023-10-08)
            // TODO: return some of the sessions back to queueManager in order to de-priorities the queue (Maxim Meshkov 2023-10-08)
            if (currentStatus == Status.Busy)
                break;
        }

        //update current state
        if (updateIsRequired)
            await processingSessions.WriteStateAsync();

        void SetMaxCapacity()
        {
            var seniority = SolutionHelper.GetAgentSeniority(this.GetPrimaryKeyString());
            var maximumConcurrency = teamConfig.Value.MaximumConcurrency;

            var seniorityDescription = teamConfig.Value.SeniorityDescriptions.FirstOrDefault(x => x.Name == seniority);

            maxCapacity = (int)Math.Floor(seniorityDescription!.Capacity * maximumConcurrency);
        }
    }

    public async Task<Status> AssignSession(string sessionId)
    {
        var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
        var sessionStatus = sessionGrain.GetStatus();

        if (sessionStatus == SessionStatus.Dead)
            return currentStatus;

        //do not take additional sessions in case it is outside the capability limit
        if (RunningSubscriptions.Count >= maxCapacity)
            return Status.Overloaded;

        var sp = this.GetStreamProvider(StreamingConst.SessionStreamName);
        var streamId = StreamId.Create(StreamingConst.SessionStreamNamespace, sessionId);
        var stream = sp.GetStream<object>(streamId);

        var subs = await stream.SubscribeAsync(async (@event, _) => { await HandleSessionEvents(sessionId, @event); });

        RunningSubscriptions.Add(sessionId, subs);

        processingSessions.State = processingSessions.State.Add(sessionId);
        await processingSessions.WriteStateAsync();

        currentStatus = CalculateCurrentStatus();
        return currentStatus;
    }

    private Status CalculateCurrentStatus() => RunningSubscriptions.Count == maxCapacity ? Status.Busy : Status.Free;

    private async Task HandleSessionEvents(string sessionId, object @event)
    {
        if (@event is SessionDeadEvent _)
        {
            await RemoveSession(sessionId);
        }
    }

    private async Task RemoveSession(string sessionId)
    {
        if (RunningSubscriptions.TryGetValue(sessionId, out var subToDispose))
        {
            processingSessions.State = processingSessions.State.Remove(sessionId);
            await processingSessions.WriteStateAsync();

            await subToDispose.UnsubscribeAsync();
            RunningSubscriptions.Remove(sessionId);

            var oldStatus = currentStatus;
            currentStatus = CalculateCurrentStatus();

            if (currentStatus != oldStatus)
            {
                var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);
                await agentManager.ChangeAgentStatus(this.GetPrimaryKeyString(), currentStatus);
            }
        }
    }

    public Task<Status> GetStatus() => Task.FromResult(currentStatus);
}