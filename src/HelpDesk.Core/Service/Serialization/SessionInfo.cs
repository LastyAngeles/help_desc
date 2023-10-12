using HelpDesk.Api.Model;
using Orleans;

namespace HelpDesk.Core.Service.Serialization;

[GenerateSerializer]

public record SessionInfo(SessionStatus Status, string AllocatedAgentId)
{
    public SessionInfo()
        : this(default, default)
    {
    }

    [Id(0)] public SessionStatus Status { get; set; } = Status;
    [Id(1)] public string AllocatedAgentId { get; set; } = AllocatedAgentId;
}