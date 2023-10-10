using System;
using System.Collections.Generic;

namespace HelpDesc.Core;

public class TeamsConfig
{
    public List<SeniorityDescription> SeniorityDescriptions { get; set; }
    public List<Team> CoreTeams { get; set; }
    public Team OverflowTeam { get; set; }
}

public class Intervals
{
    public TimeSpan SessionPollInterval { get; set; }
    public int MaxMissingPolls { get; set; }
    public int MaximumConcurrency { get; set; }
    public double MaximumQueueCapacityMultiplier { get; set; }
}

public class SeniorityDescription
{
    public string Name { get; set; }
    public string Description { get; set; }
    public int Priority { get; set; }
    public double Capacity { get; set; }
}

public class Team
{
    public string Name { get; set; }
    /// <summary>Seniority system name to the number of members.</summary>
    public Dictionary<string, int> Stuff { get; set; }
    public TimeSpan StartWork { get; set; }
    public TimeSpan EndWork { get; set; }
}