using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IAgentManagerGrain: IGrainWithStringKey
{
    Task<Agent> AssignAgent(string sessionId);
}