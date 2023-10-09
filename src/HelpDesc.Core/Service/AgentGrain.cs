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

namespace HelpDesc.Core.Service;

public record AgentInfo(int Capacity);

public class AgentGrain : Grain, IAgentGrain
{
    private readonly IOptions<TeamsConfig> teamConfig;
    private readonly IPersistentState<ImmutableList<string>> processingSessions;
    private readonly IPersistentState<AgentInfo> agentInfo;

    //sessionId => subscription
    public Dictionary<string, StreamSubscriptionHandle<object>> RunningSubscriptions { get; set; } = new();

    private Status currentStatus;

    public AgentGrain(IOptions<TeamsConfig> teamConfig,
        [PersistentState("agentsSessions", SolutionConst.HelpDescStore)]
        IPersistentState<ImmutableList<string>> processingSessions,
        [PersistentState("agents", SolutionConst.HelpDescStore)]
        IPersistentState<AgentInfo> agentInfo)
    {
        this.teamConfig = teamConfig;
        this.processingSessions = processingSessions;
        this.agentInfo = agentInfo;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var sessionIds = processingSessions.State;
        if (!agentInfo.RecordExists)
        {
            agentInfo.State = new AgentInfo(GetMaxCapacity());
            await agentInfo.WriteStateAsync();
        }

        var updateIsRequired = false;

        foreach (var sessionId in sessionIds)
        {
            var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
            var sessionStatus = await sessionGrain.GetStatus();
            if (sessionStatus == SessionStatus.Dead)
            {
                updateIsRequired = true;
                processingSessions.State = sessionIds.Remove(sessionId);
                continue;
            }

            currentStatus = await AssignSession(sessionId, false);

            if (currentStatus is Status.Busy or Status.Closing)
                break;
        }

        //update current state
        if (updateIsRequired)
            await processingSessions.WriteStateAsync();
    }

    private int GetMaxCapacity()
    {
        var seniority = SolutionHelper.GetAgentSeniority(this.GetPrimaryKeyString());
        var maximumConcurrency = teamConfig.Value.MaximumConcurrency;

        var seniorityDescription = teamConfig.Value.SeniorityDescriptions.FirstOrDefault(x => x.Name == seniority);

        return (int)Math.Floor(seniorityDescription!.Capacity * maximumConcurrency);
    }

    public Task<Status> AssignSession(string sessionId)
    {
        return AssignSession(sessionId, true);
    }

    private async Task<Status> AssignSession(string sessionId, bool shouldSaveState)
    {
        var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
        var sessionStatus = await sessionGrain.GetStatus();

        if (sessionStatus == SessionStatus.Dead || currentStatus is Status.Busy or Status.Closing)
            return currentStatus;

        //do not take additional sessions in case it is outside the capability limit
        //pathological case, because AgentManager should take care about this scenario
        if (RunningSubscriptions.Count >= agentInfo.State.Capacity)
            return Status.Overloaded;

        var sp = this.GetStreamProvider(SolutionConst.StreamProviderName);
        var streamId = StreamId.Create(SolutionConst.SessionStreamNamespace, sessionId);
        var stream = sp.GetStream<object>(streamId);

        var subs = await stream.SubscribeAsync(async (@event, _) => { await HandleSessionEvents(sessionId, @event); });

        RunningSubscriptions.Add(sessionId, subs);

        if (shouldSaveState)
        {
            processingSessions.State = processingSessions.State.Add(sessionId);
            await processingSessions.WriteStateAsync();
        }

        currentStatus = CalculateCurrentStatus();
        return currentStatus;
    }

    private Status CalculateCurrentStatus() =>
        RunningSubscriptions.Count == agentInfo.State.Capacity ? Status.Busy : Status.Free;

    private async Task HandleSessionEvents(string sessionId, object @event)
    {
        if (@event is SessionDeadEvent _)
            await RemoveSession(sessionId);
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

            if (oldStatus != Status.Closing && currentStatus != oldStatus)
            {
                var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);
                await agentManager.ChangeAgentStatus(this.GetPrimaryKeyString(), currentStatus);
            }
        }
    }

    public Task<Status> GetStatus() => Task.FromResult(currentStatus);

    public Task CloseAgent()
    {
        currentStatus = Status.Closing;
        return Task.CompletedTask;
    }
}