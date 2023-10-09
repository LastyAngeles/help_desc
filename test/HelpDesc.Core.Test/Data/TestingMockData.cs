using System;
using System.Collections.Generic;

namespace HelpDesc.Core.Test.Data;

public class TestingMockData
{
    public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
    public static readonly int MaxMissingPolls = 3;
    public static readonly double MaxQueueCapacityMultiplier = 1.5;
    public static readonly int MaxConcurrency = 10;

    public const string JuniorSystemName = "jnr";
    public const string MiddleSystemName = "mdl";
    public const string SeniorSystemName = "snr";
    public const string TeamLeadSystemName = "tld";

    public static List<SeniorityDescription> SeniorityDescriptions { get; set; } = new()
    {
        new SeniorityDescription(JuniorSystemName, "Junior", 1, 0.4),
        new SeniorityDescription(MiddleSystemName, "Middle", 2, 0.6),
        new SeniorityDescription(SeniorSystemName, "Senior", 3, 0.8),
        new SeniorityDescription(TeamLeadSystemName, "Team-Lead", 4, 0.5)
    };

    public static List<Team> CoreTeams { get; set; } = new()
    {
        new Team("Team A", new Dictionary<string, int>
        {
            { JuniorSystemName, 1 },
            { MiddleSystemName, 2 },
            { TeamLeadSystemName, 1 }
        }, TimeSpan.Parse("00:00:00"), TimeSpan.Parse("08:00:00")),

        new Team("Team B", new Dictionary<string, int>
        {
            { JuniorSystemName, 2 },
            { MiddleSystemName, 1 },
            { SeniorSystemName, 1 }
        }, TimeSpan.Parse("08:00:00"), TimeSpan.Parse("16:00:00")),

        new Team("Team C", new Dictionary<string, int>
        {
            { MiddleSystemName, 2 }
        }, TimeSpan.Parse("08:00:00"), TimeSpan.Parse("24:00:00")),
    };

    public static Team OverflowTeam { get; set; } = new("Overflow", new Dictionary<string, int>
    {
        { JuniorSystemName, 6 }
    }, TimeSpan.Parse("00:00:00"), TimeSpan.Parse("24:00:00"));
}