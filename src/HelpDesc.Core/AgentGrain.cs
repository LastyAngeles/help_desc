using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Core;

public class AgentGrain : Grain, IAgentGrain
{
    public Task<Status> AssignSession(string sessionId)
    {
        throw new System.NotImplementedException();
    }

    public Task<Status> GetStatus()
    {
        // TODO: implement (Maxim Meshkov 2023-10-08)
        return Task.FromResult(Status.Free);
    }
}