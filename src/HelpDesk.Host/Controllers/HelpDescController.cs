using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesk.Api;
using HelpDesk.Api.Model;
using HelpDesk.Host.Controllers.Model;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Orleans;

namespace HelpDesk.Host.Controllers;

[ApiController]
[Route("api/helpdesk")]
public class HelpDeskController : ControllerBase
{
    private const string PrimaryGrainId = "Worker";

    private readonly IClusterClient orleansClient;
    private readonly ILogger<HelpDeskController> logger;

    public HelpDeskController(IClusterClient orleansClient, ILogger<HelpDeskController> logger)
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

        var queueManager = orleansClient.GetGrain<IQueueManagerGrain>(PrimaryGrainId);
        return await queueManager.CreateSession();
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
        var queueManager = orleansClient.GetGrain<IQueueManagerGrain>(PrimaryGrainId);
        return await queueManager.GetQueuedSessions();
    }

    [HttpGet("team/core")]
    public async Task<ImmutableList<Agent>> GetCoreTeam()
    {
        var agentManager = orleansClient.GetGrain<IAgentManagerGrain>(PrimaryGrainId);
        return await agentManager.GetCoreTeam();
    }

    [HttpGet("team/overflow")]
    public async Task<ImmutableList<Agent>> GetOverflowTeam()
    {
        var queueManager = orleansClient.GetGrain<IAgentManagerGrain>(PrimaryGrainId);
        return await queueManager.GetOverflowTeam();
    }

    [HttpGet("team/overall")]
    public async Task<TeamDto> GetTeamComposition()
    {
        var agentManager = orleansClient.GetGrain<IAgentManagerGrain>(PrimaryGrainId);

        var coreTeam = await agentManager.GetCoreTeam();
        var overflowTeam = await agentManager.GetOverflowTeam();
        var currentTeamName = await agentManager.GetCurrentTeamName();
        var maxQueueCapacity = await agentManager.GetMaxQueueCapacity();

        return new TeamDto(coreTeam, overflowTeam, currentTeamName, maxQueueCapacity);
    }
}