using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Core.Service.Serialization;

[GenerateSerializer]

public record SessionInfo(SessionStatus Status, string AllocatedAgentId)
{
    [Id(0)] public SessionStatus Status { get; set; } = Status;
    [Id(1)] public string AllocatedAgentId { get; set; } = AllocatedAgentId;
}