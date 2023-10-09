using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IAgentGrain : IGrainWithStringKey
{
    Task<AgentStatus> AssignSession(string sessionId);
    Task<AgentStatus> GetStatus();
    Task CloseAgent();
    Task<ImmutableList<string>> GetCurrentSessionIds();
}