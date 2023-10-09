using Orleans;

namespace HelpDesc.Api.Model;

[GenerateSerializer]
public record Agent(string Id, string Seniority, int Priority, AgentStatus Availability)
{
    [Id(0)] public string Id { get; set; } = Id;
    [Id(0)] public string Seniority { get; set; } = Seniority;
    [Id(0)] public int Priority { get; set; } = Priority;
    [Id(0)] public AgentStatus Availability { get; set; } = Availability;
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