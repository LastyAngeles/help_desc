using System.Threading.Tasks;
using HelpDesk.Api.Model;
using Orleans;

namespace HelpDesk.Api;

public interface ISessionGrain : IGrainWithStringKey
{
    Task ChangeStatus(SessionStatus status);

    Task<SessionStatus> GetStatus();

    Task<SessionStatus> AllocateAgent(string agentId);

    Task<string> GetAllocatedAgentId();
}