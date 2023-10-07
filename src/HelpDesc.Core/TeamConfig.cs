using System;
using System.Collections.Generic;

namespace HelpDesc.Core;

public record TeamsConfig
{
    public int MaximumConcurrency { get; set; }

    public List<SeniorityDescription> SeniorityDescriptions { get; set; }

    public List<Team> CoreTeams { get; set; }

    public Team OverflowTeam { get; set; }
}

public record SeniorityDescription(string Name, int Priority, double Capacity);

public record Team(string Name, Dictionary<string, int> Stuff, TimeSpan StartWork, TimeSpan EndWork);