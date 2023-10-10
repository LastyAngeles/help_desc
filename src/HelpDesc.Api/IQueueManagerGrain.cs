using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IQueueManagerGrain : IGrainWithStringKey
{
    Task<SessionCreationResult> CreateSession();
    Task<ImmutableList<string>> GetQueuedSessions();
}