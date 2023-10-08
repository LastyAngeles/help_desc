using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface ISessionGrain : IGrainWithStringKey
{
    public SessionStatus GetStatus();
}