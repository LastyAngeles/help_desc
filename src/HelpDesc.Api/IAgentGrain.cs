using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IAgentGrain : IGrainWithStringKey
{
    Task<Status> AssignSession(string sessionId);
    Task<Status> GetStatus();
}