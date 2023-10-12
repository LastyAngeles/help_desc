using System.Collections.Immutable;
using Orleans;

namespace HelpDesk.Api.Model;

[GenerateSerializer]
public record Agent(string Id, string Seniority, int Priority, AgentStatus Availability, double Capacity, int MaxCapacity)
{
    [Id(0)] public string Id { get; set; } = Id;
    [Id(1)] public string Seniority { get; set; } = Seniority;
    [Id(2)] public int Priority { get; set; } = Priority;
    [Id(3)] public AgentStatus Availability { get; set; } = Availability;
    [Id(4)] public double Capacity { get; set; } = Capacity;
    [Id(5)] public int MaxCapacity { get; set; } = MaxCapacity;
    [Id(6)] public int Workload { get; set; }
    [Id(7)] public ImmutableList<string> RunningSessions { get; set; }
}

public enum AgentStatus
{
    Busy,
    Free,
    Overloaded,
    Closing
}

public enum SessionStatus
{
    Alive,
    Disconnected,
    Dead
}