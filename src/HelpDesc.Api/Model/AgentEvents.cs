using Orleans;

namespace HelpDesc.Api.Model;

[GenerateSerializer]
public record AgentEvent(string AgentId)
{
    [Id(0)] public string AgentId { get; set; } = AgentId;
}

public record AgentIsDisposing(string AgentId) : AgentEvent(AgentId);