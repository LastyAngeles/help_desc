using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesk.Api.Model;
using Orleans;

namespace HelpDesk.Api;

public interface IAgentManagerGrain: IGrainWithStringKey
{
    Task<Agent> AssignAgent(string sessionId);
    Task<double> GetMaxQueueCapacity();
    Task<ImmutableList<Agent>> GetCoreTeam();
    Task<ImmutableList<Agent>> GetOverflowTeam();
    Task<string> GetCurrentTeamName();
    Task ForceShift();
}