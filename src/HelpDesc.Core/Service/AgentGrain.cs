using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core.Extensions;
using HelpDesc.Core.Service.Serialization;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace HelpDesc.Core.Service;

public class AgentGrain : Grain, IAgentGrain
{
    private readonly IOptions<TeamsConfig> teamConfig;
    private readonly IPersistentState<AgentInfo> agentInfo;

    //sessionId => subscription
    public Dictionary<string, StreamSubscriptionHandle<object>> RunningSubscriptions { get; set; } = new();

    private AgentStatus currentStatus;

    public AgentGrain(IOptions<TeamsConfig> teamConfig,
        [PersistentState("agentsInfo", SolutionConst.HelpDescStore)]
        IPersistentState<AgentInfo> agentInfo)
    {
        this.teamConfig = teamConfig;
        this.agentInfo = agentInfo;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (!agentInfo.RecordExists)
        {
            agentInfo.State = new AgentInfo(ImmutableList<string>.Empty, GetMaxCapacity());
            await agentInfo.WriteStateAsync();
        }

        var sessionIds = agentInfo.State.SessionIds;

        //if agent was busy before deactivation, it would not after
        //tasks would be re-assigned back to the agent
        currentStatus = agentInfo.State.Status == AgentStatus.Closing ? agentInfo.State.Status : AgentStatus.Free;

        foreach (var sessionId in sessionIds)
        {
            var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
            var sessionStatus = await sessionGrain.GetStatus();
            if (sessionStatus == SessionStatus.Dead)
            {
                agentInfo.State.SessionIds = sessionIds.Remove(sessionId);
                continue;
            }

            //event if agent is closing, it is still required to re-assign existed task in order to finish them
            currentStatus = await AssignSession(sessionId, false, false);

            if (currentStatus is AgentStatus.Busy)
                break;
        }

        agentInfo.State.Status = currentStatus;
        await agentInfo.WriteStateAsync();
    }

    private int GetMaxCapacity()
    {
        var seniority = SolutionHelper.GetAgentSeniority(this.GetPrimaryKeyString());
        var maximumConcurrency = teamConfig.Value.MaximumConcurrency;

        var seniorityDescription = teamConfig.Value.SeniorityDescriptions.FirstOrDefault(x => x.Name == seniority);

        return (int)Math.Floor(seniorityDescription!.Capacity * maximumConcurrency);
    }

    public Task<AgentStatus> AssignSession(string sessionId)
    {
        return AssignSession(sessionId, true);
    }

    private async Task<AgentStatus> AssignSession(string sessionId, bool shouldSaveState, bool respectClosing = true)
    {
        var sessionGrain = GrainFactory.GetGrain<ISessionGrain>(sessionId);
        var sessionStatus = await sessionGrain.GetStatus();

        if (sessionStatus == SessionStatus.Dead || currentStatus is AgentStatus.Busy &&
            (respectClosing && currentStatus == AgentStatus.Closing))
            return currentStatus;

        //do not take additional sessions in case it is outside the capability limit
        //pathological case, because AgentManager should take care about this scenario
        if (RunningSubscriptions.Count >= agentInfo.State.Capacity)
            return AgentStatus.Overloaded;

        var stream = SolutionHelper.GetStream(this.GetStreamProvider(SolutionConst.StreamProviderName), sessionId,
            SolutionConst.SessionStreamNamespace);

        var subs = await stream.SubscribeAsync((@event, _) => HandleSessionEvents(sessionId, @event));

        RunningSubscriptions.Add(sessionId, subs);

        if (shouldSaveState)
        {
            agentInfo.State.SessionIds = agentInfo.State.SessionIds.Add(sessionId);
            await agentInfo.WriteStateAsync();
        }

        var oldStatus = currentStatus;
        currentStatus = CalculateCurrentStatus();

        if (oldStatus != currentStatus)
        {
            await agentInfo.WriteStateAsync();
            agentInfo.State.Status = currentStatus;
        }

        return currentStatus;
    }

    private AgentStatus CalculateCurrentStatus() =>
        RunningSubscriptions.Count == agentInfo.State.Capacity ? AgentStatus.Busy : AgentStatus.Free;

    private async Task HandleSessionEvents(string sessionId, object @event)
    {
        if (@event is SessionDeadEvent _)
            await RemoveSession(sessionId);
    }

    private async Task RemoveSession(string sessionId)
    {
        if (RunningSubscriptions.TryGetValue(sessionId, out var subToDispose))
        {
            agentInfo.State.SessionIds = agentInfo.State.SessionIds.Remove(sessionId);
            await agentInfo.WriteStateAsync();

            await subToDispose.UnsubscribeAsync();
            RunningSubscriptions.Remove(sessionId);

            var oldStatus = currentStatus;
            currentStatus = CalculateCurrentStatus();

            if (oldStatus != AgentStatus.Closing && currentStatus != oldStatus)
            {
                agentInfo.State.Status = currentStatus;
                await agentInfo.WriteStateAsync();

                var agentManager = GrainFactory.GetGrain<IAgentManagerGrain>(0);
                await agentManager.ChangeAgentStatus(this.GetPrimaryKeyString(), currentStatus);
            }
        }
    }

    public Task<AgentStatus> GetStatus() => Task.FromResult(currentStatus);

    public async Task CloseAgent()
    {
        currentStatus = AgentStatus.Closing;
        agentInfo.State.Status = currentStatus;
        await agentInfo.WriteStateAsync();
    }
}