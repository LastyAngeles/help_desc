using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;

namespace HelpDesc.Host.Controllers;

[ApiController]
[Route("api/helpdesc")]
public class HelpDescController : ControllerBase
{
    private readonly IClusterClient orleansClient;
    private readonly ILogger<HelpDescController> logger;

    public HelpDescController(IClusterClient orleansClient, ILogger<HelpDescController> logger)
    {
        this.orleansClient = orleansClient;
        this.logger = logger;
    }

    [HttpPost("session")]
    public async Task<SessionCreationResult> OpenSession([FromQuery] string sessionId = default)
    {
        if (sessionId != null)
        {
            var sessionGrain = orleansClient.GetGrain<ISessionGrain>(sessionId);

            if (await sessionGrain.GetStatus() == SessionStatus.Dead)
            {
                var exception = $"Attempt to create session which was already dead. Dead session id: {sessionId}";
                logger.LogError(exception);
                return new SessionCreationResult(sessionId, false) { ExceptionMessage = exception };
            }

            await sessionGrain.ChangeStatus(SessionStatus.Alive);
            return new SessionCreationResult(sessionId, true);
        }

        var grain = orleansClient.GetGrain<IQueueManagerGrain>(0);
        return await grain.CreateSession();
    }

    [HttpDelete("session")]
    public async Task CloseSession([FromQuery] string sessionId)
    {
        var sessionGrain = orleansClient.GetGrain<ISessionGrain>(sessionId);
        await sessionGrain.ChangeStatus(SessionStatus.Disconnected);
    }

    [HttpGet("sessions")]
    public async Task<ImmutableList<string>> GetQueuedSessions()
    {
        var queueManager = orleansClient.GetGrain<IQueueManagerGrain>(0);
        return await queueManager.GetQueuedSessions();
    }

    [HttpGet("team/core")]
    public async Task<ImmutableList<Agent>> GetCoreTeam()
    {
        var queueManager = orleansClient.GetGrain<IAgentManagerGrain>(0);
        return await queueManager.GetCoreTeam();
    }

    [HttpGet("team/overflow")]
    public async Task<ImmutableList<Agent>> GetOverflowTeam()
    {
        var queueManager = orleansClient.GetGrain<IAgentManagerGrain>(0);
        return await queueManager.GetOverflowTeam();
    }
}