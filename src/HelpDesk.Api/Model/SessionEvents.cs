using Orleans;

namespace HelpDesk.Api.Model;

// TODO: inherit from base for more appropriate handling (Maxim Meshkov 2023-10-08)
[GenerateSerializer]
public record SessionDeadEvent;