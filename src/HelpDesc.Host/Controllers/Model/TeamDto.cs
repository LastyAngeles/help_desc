using System.Collections.Immutable;
using HelpDesc.Api.Model;

namespace HelpDesc.Host.Controllers.Model;

public record TeamDto(ImmutableList<Agent> CoreTeam, ImmutableList<Agent> OverflowTeam, string CoreTeamName,
    double MaxQueueCapacity);