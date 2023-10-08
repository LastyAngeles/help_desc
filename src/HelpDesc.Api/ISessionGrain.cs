using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface ISessionGrain : IGrainWithStringKey
{
    Task ChangeStatus(SessionStatus status);

    Task<SessionStatus> GetStatus();
}