using System.Collections.Immutable;
using HelpDesk.Api.Model;

namespace HelpDesk.Host.Controllers.Model;

public record TeamDto(ImmutableList<Agent> CoreTeam, ImmutableList<Agent> OverflowTeam, string CoreTeamName,
    double MaxQueueCapacity);