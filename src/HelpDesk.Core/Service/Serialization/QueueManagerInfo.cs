using System.Collections.Immutable;
using Orleans;

namespace HelpDesk.Core.Service.Serialization;

[GenerateSerializer]
public record QueueManagerInfo(ImmutableList<string> SessionIds)
{
    public QueueManagerInfo()
        : this(ImmutableList<string>.Empty)
    {
    }

    [Id(0)] public ImmutableList<string> SessionIds { get; set; } = SessionIds;
}