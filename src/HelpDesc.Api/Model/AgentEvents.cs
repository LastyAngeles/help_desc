using Orleans;

namespace HelpDesc.Api.Model;

[GenerateSerializer]
public record AgentEvent(string AgentId)
{
    public AgentEvent()
        : this(string.Empty)
    {
    }

    [Id(0)] public string AgentId { get; set; } = AgentId;
}

[GenerateSerializer]
public record AgentIsDisposing(string AgentId) : AgentEvent(AgentId)
{
    public AgentIsDisposing()
        : this(string.Empty)
    {
    }
}