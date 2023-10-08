using Orleans;

namespace HelpDesc.Api.Model;

[GenerateSerializer]
public record SessionCreationResult(string Id, bool Success)
{
    [Id(0)] public string Id { get; set; } = Id;

    [Id(1)] public bool Success { get; set; } = Success;

    [Id(2)] public string ExceptionMessage { get; set; }
}