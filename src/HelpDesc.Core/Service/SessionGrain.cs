using System;
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
    // ReSharper disable once PrivateFieldCanBeConvertedToLocalVariable
    private readonly TeamsConfig config;
    private readonly IPersistentState<SessionStatus> sessionStatus;
    private readonly IDisposable timerDispose;
    private int missingPollCount;

    public SessionGrain(IOptions<TeamsConfig> config,
        [PersistentState("sessions", "helpDescStore")]
        IPersistentState<SessionStatus> sessionStatus)
    {
        this.config = config.Value;
        this.sessionStatus = sessionStatus;

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
                        if (missingPollCount >= this.config.MaxMissingPolls)
                        {
                            sessionStatus.State = SessionStatus.Dead;
                            await sessionStatus.WriteStateAsync();


                            var sp = this.GetStreamProvider(StreamingConst.SessionStreamName);
                            var streamId = StreamId.Create(StreamingConst.SessionStreamNamespace,
                                this.GetPrimaryKeyString());
                            var stream = sp.GetStream<object>(streamId);
                            await stream.OnNextAsync(new SessionDeadEvent());

                            timerDispose!.Dispose();
                        }
                    }
                    else
                    {
                        timerDispose!.Dispose();
                    }
                }
            }, null, TimeSpan.Zero, this.config.SessionPollInterval);
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