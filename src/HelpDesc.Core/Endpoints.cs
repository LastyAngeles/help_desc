using Orleans.Configuration;

namespace HelpDesc.Core;

public class Endpoints
{
    public int SiloPort { get; set; } = EndpointOptions.DEFAULT_SILO_PORT;
    public int GatewayPort { get; set; } = EndpointOptions.DEFAULT_GATEWAY_PORT;
}