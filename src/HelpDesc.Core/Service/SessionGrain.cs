using System;
using System.Threading;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core.Extensions;
using HelpDesc.Core.Service.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace HelpDesc.Core.Service;

public class SessionGrain : Grain, ISessionGrain
{
    private readonly TeamsConfig config;
    private readonly IPersistentState<SessionInfo> sessionInfo;
    private readonly ILogger<SessionGrain> logger;
    private IDisposable timerDispose;
    private int missingPollCount;

    private StreamSubscriptionHandle<object> agentSubs;

    public SessionGrain(IOptions<TeamsConfig> config,
        [PersistentState("sessions", SolutionConst.HelpDescStore)]
        IPersistentState<SessionInfo> sessionInfo,
        ILogger<SessionGrain> logger)
    {
        this.config = config.Value;
        this.sessionInfo = sessionInfo;
        this.logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (!sessionInfo.RecordExists)
            sessionInfo.State.Status = SessionStatus.Alive;

        if (sessionInfo.State.Status == SessionStatus.Alive)
        {
            if (!string.IsNullOrEmpty(sessionInfo.State.AllocatedAgentId))
            {
                var agentStream =
                    this.GetStream(sessionInfo.State.AllocatedAgentId, SolutionConst.AgentStreamNamespace);
                agentSubs = await agentStream.SubscribeAsync(HandleAgentEvents);
            }

            timerDispose = RegisterTimer(_ => TimerTick(), null, config.SessionPollInterval, config.SessionPollInterval);
        }

        async Task TimerTick()
        {
            if (sessionInfo.State.Status == SessionStatus.Alive)
                missingPollCount = 0;
            else
            {
                if (sessionInfo.State.Status == SessionStatus.Disconnected)
                {
                    missingPollCount++;
                    if (missingPollCount >= config.MaxMissingPolls)
                    {
                        sessionInfo.State.Status = SessionStatus.Dead;
                        sessionInfo.State.AllocatedAgentId = null;
                        await agentSubs.UnsubscribeAsync();
                        await sessionInfo.WriteStateAsync();

                        var stream = this.GetStream(this.GetPrimaryKeyString(),
                            SolutionConst.SessionStreamNamespace);
                        await stream.OnNextAsync(new SessionDeadEvent());

                        timerDispose.Dispose();
                    }
                }
                else
                {
                    timerDispose.Dispose();
                }
            }
        }
    }

    private async Task HandleAgentEvents(object @event)
    {
        if (@event is not AgentEvent agentEvent)
            //ignore
            return;

        if (agentEvent.AgentId != sessionInfo.State.AllocatedAgentId)
        {
            logger.LogWarning(
                "Session with id: {SessionId} received message from different agent with id: {ForeignAgentId}." +
                "Event would be ignored." +
                "Current agent id: {AgentId}", this.GetPrimaryKeyString(), agentEvent.AgentId,
                sessionInfo.State.AllocatedAgentId);
            return;
        }

        switch (@event)
        {
            case AgentIsDisposing _:
                sessionInfo.State.AllocatedAgentId = null;
                await sessionInfo.WriteStateAsync();
                if (agentSubs != null)
                    await agentSubs.UnsubscribeAsync();
                break;
        }
    }

    public async Task ChangeStatus(SessionStatus status)
    {
        if (sessionInfo.State.Status == SessionStatus.Dead)
            return;

        sessionInfo.State.Status = status;
        await sessionInfo.WriteStateAsync();
    }

    public Task<SessionStatus> GetStatus() => Task.FromResult(sessionInfo.State.Status);

    public async Task<SessionStatus> AllocateAgent(string agentId)
    {
        if(sessionInfo.State.Status == SessionStatus.Dead)
        {
            logger.LogWarning("Attempt to allocate agent to the dead session. Request ignored. Session id: {Id}. Agent id: {AgentId}", this.GetPrimaryKeyString(), agentId);
            return sessionInfo.State.Status;
        }

        if (string.IsNullOrEmpty(agentId))
        {
            logger.LogWarning("Attempt to allocate agent with null or empty id. Request ignored. Session id: {Id}.", this.GetPrimaryKeyString());
            return sessionInfo.State.Status;
        }

        sessionInfo.State.AllocatedAgentId = agentId;
        await sessionInfo.WriteStateAsync();
        return sessionInfo.State.Status;
    }

    public override async Task OnDeactivateAsync(DeactivationReason reason, CancellationToken cancellationToken)
    {
        sessionInfo.State.Status = SessionStatus.Disconnected;
        await sessionInfo.WriteStateAsync();

        await base.OnDeactivateAsync(reason, cancellationToken);
    }
}