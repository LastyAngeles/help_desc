using Orleans;
using System.Collections.Immutable;
using HelpDesc.Api.Model;

namespace HelpDesc.Core.Service.Serialization;

[GenerateSerializer]
public record AgentInfo(ImmutableList<string> SessionIds, int Capacity)
{
    public AgentInfo()
        : this(default, default)
    {
    }

    [Id(0)] public ImmutableList<string> SessionIds { get; set; } = SessionIds;
    [Id(1)] public int Capacity { get; set; } = Capacity;
    [Id(2)] public AgentStatus Status { get; set; }
}