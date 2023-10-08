using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
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
        var grain = orleansClient.GetGrain<IQueueManagerGrain>(0);
        return await grain.CreateSession();
    }

    [HttpDelete("session")]
    public Task CloseSession([FromQuery] string sessionId)
    {
        // TODO: implement (Maxim Meshkov 2023-10-07)
        return Task.CompletedTask;
    }
}