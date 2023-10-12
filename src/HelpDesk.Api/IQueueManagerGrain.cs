using System.Collections.Immutable;
using System.Threading.Tasks;
using HelpDesk.Api.Model;
using Orleans;

namespace HelpDesk.Api;

public interface IQueueManagerGrain : IGrainWithStringKey
{
    Task<SessionCreationResult> CreateSession();
    Task<ImmutableList<string>> GetQueuedSessions();
}