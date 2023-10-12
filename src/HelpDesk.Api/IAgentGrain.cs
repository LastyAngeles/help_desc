using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesk.Api.Model;
using Orleans;

namespace HelpDesk.Api;

public interface IAgentGrain : IGrainWithStringKey
{
    Task<AgentStatus> AssignSession(string sessionId);
    Task<AgentStatus> GetStatus();
    Task CloseAgent();
    Task<ImmutableList<string>> GetCurrentSessionIds();
    Task<int> GetCurrentWorkload();
}