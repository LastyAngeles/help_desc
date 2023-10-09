using System;
using System.Threading;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core.Extensions;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;

namespace HelpDesc.Core.Service;

public class SessionGrain : Grain, ISessionGrain
{
    private readonly TeamsConfig config;
    private readonly IPersistentState<SessionStatus> sessionStatus;
    private IDisposable timerDispose;
    private int missingPollCount;

    public SessionGrain(IOptions<TeamsConfig> config,
        [PersistentState("sessions", SolutionConst.HelpDescStore)]
        IPersistentState<SessionStatus> sessionStatus)
    {
        this.config = config.Value;
        this.sessionStatus = sessionStatus;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        if (!sessionStatus.RecordExists)
            sessionStatus.State = SessionStatus.Alive;

        if (sessionStatus.State == SessionStatus.Alive)
        {
            timerDispose = RegisterTimer(async _ =>
            {
                if (sessionStatus.State == SessionStatus.Alive)
                {
                    missingPollCount = 0;
                }
                else
                {
                    if (sessionStatus.State == SessionStatus.Disconnected)
                    {
                        missingPollCount++;
                        if (missingPollCount >= config.MaxMissingPolls)
                        {
                            sessionStatus.State = SessionStatus.Dead;
                            await sessionStatus.WriteStateAsync();


                            var sp = this.GetStreamProvider(SolutionConst.StreamProviderName);
                            var streamId = StreamId.Create(SolutionConst.SessionStreamNamespace,
                                this.GetPrimaryKeyString());
                            var stream = sp.GetStream<object>(streamId);
                            await stream.OnNextAsync(new SessionDeadEvent());

                            timerDispose.Dispose();
                        }
                    }
                    else
                    {
                        timerDispose.Dispose();
                    }
                }
            }, null, config.SessionPollInterval, config.SessionPollInterval);
        }
    }

    public async Task ChangeStatus(SessionStatus status)
    {
        if (sessionStatus.State == SessionStatus.Dead)
            return;

        sessionStatus.State = status;
        await sessionStatus.WriteStateAsync();
    }

    public Task<SessionStatus> GetStatus() => Task.FromResult(sessionStatus.State);
}