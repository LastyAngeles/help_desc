using Orleans;

namespace HelpDesc.Api.Model;

[GenerateSerializer]
public class SessionCreateResult
{
    public SessionCreateResult(string id, bool success)
    {
        Id = id;
        Success = success;
    }

    [Id(0)] public string Id { get; set; }

    [Id(1)] public bool Success { get; set; }
}