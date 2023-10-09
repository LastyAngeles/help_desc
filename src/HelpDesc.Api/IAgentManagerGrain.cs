using System.Collections.Generic;
using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IAgentManagerGrain: IGrainWithIntegerKey
{
    Task<Agent> AssignAgent(string sessionId);
    Task<double> GetMaxQueueCapacity();
    Task ChangeAgentStatus(string agentId, AgentStatus status);
    Task<List<Agent>> GetCoreTeam();
    Task<List<Agent>> GetOverflowTeam();
}