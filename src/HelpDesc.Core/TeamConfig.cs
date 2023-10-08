﻿using System;
using System.Collections.Generic;

namespace HelpDesc.Core;

public record TeamsConfig
{
    // TODO: split into different config! (Maxim Meshkov 2023-10-09)
    public TimeSpan SessionPollInterval { get; set; }

    public int MaxMissingPolls { get; set; }

    public int MaximumConcurrency { get; set; }

    public double MaximumQueueCapacityMultiplier { get; set; }

    public List<SeniorityDescription> SeniorityDescriptions { get; set; }

    public List<Team> CoreTeams { get; set; }

    public Team OverflowTeam { get; set; }
}

public record SeniorityDescription(string Name, string Description, int Priority, double Capacity);

/// <param name="Stuff">Seniority system name to the number of members.</param>
public record Team(string Name, Dictionary<string, int> Stuff, TimeSpan StartWork, TimeSpan EndWork);