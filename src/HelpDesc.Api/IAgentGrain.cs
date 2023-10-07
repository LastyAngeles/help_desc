using System.Threading.Tasks;
using Orleans;

namespace HelpDesc.Api;

public interface IAgentGrain : IGrainWithStringKey
{
    Task<bool> AssignSession(string sessionId);
}