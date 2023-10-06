using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace HelpDesc.Host.Controllers;

[ApiController]
[Route("api/helpdesc")]
public class HelpDescController : ControllerBase
{
    private readonly ILogger<HelpDescController> logger;

    public HelpDescController(ILogger<HelpDescController> logger)
    {
        this.logger = logger;
    }
}