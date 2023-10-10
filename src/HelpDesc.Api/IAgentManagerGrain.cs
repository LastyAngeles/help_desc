using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IAgentManagerGrain: IGrainWithStringKey
{
    Task<Agent> AssignAgent(string sessionId);
    Task<double> GetMaxQueueCapacity();
    Task<ImmutableList<Agent>> GetCoreTeam();
    Task<ImmutableList<Agent>> GetOverflowTeam();
    Task<string> GetCurrentTeamName();
    Task ForceShift();
}