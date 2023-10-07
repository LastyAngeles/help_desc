using System.Threading.Tasks;
using HelpDesc.Api;
using Orleans;

namespace HelpDesc.Core;

public class AgentGrain : Grain, IAgentGrain
{
    public Task<bool> AssignSession(string sessionId)
    {
        throw new System.NotImplementedException();
    }
}