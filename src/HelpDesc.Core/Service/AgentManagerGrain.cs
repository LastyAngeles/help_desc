using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HelpDesc.Api;
using HelpDesc.Api.Model;
using HelpDesc.Core.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace HelpDesc.Core.Service;

[ImplicitStreamSubscription(SolutionConst.AgentManagerStreamNamespace)]
public class AgentManagerGrain : Grain, IAgentManagerGrain, IRemindable
{
    private readonly ITimeProvider timeProvider;
    private readonly ILogger<AgentManagerGrain> logger;
    private readonly TeamsConfig teamsConfig;
    private readonly Intervals intervals;

    //priority => agent
    private Dictionary<int, List<Agent>> CoreAgentPool { get; } = new();
    private Dictionary<int, List<Agent>> OverflowAgentPool { get; } = new();

    //all agents together
    private List<Agent> AgentsPool { get; set; }

    private string currentTeamName;

    //priority => last allocated id (for round robin)
    private Dictionary<int, string> CorePriorityRoundRobinMap { get; } = new();
    private Dictionary<int, string> OverflowPriorityRoundRobinMap { get; } = new();

    private double maxQueueCapacity;
    private double maxQueueCapacityMultiplier;

    private const string TeamShiftReminderName = "teamShift";

    public AgentManagerGrain(IOptions<TeamsConfig> teamConfigOptions, IOptions<Intervals> intervalsOptions,
        ITimeProvider timeProvider,
        ILogger<AgentManagerGrain> logger)
    {
        this.timeProvider = timeProvider;
        teamsConfig = teamConfigOptions.Value;
        intervals = intervalsOptions.Value;
        this.logger = logger;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var currentTeam = AllocateCurrentTeam();
        if (currentTeam == null)
            return;

        currentTeamName = currentTeam.Name;

        var agentManagerStream = this.GetStream(this.GetPrimaryKeyString(), SolutionConst.AgentManagerStreamNamespace);
        await agentManagerStream.SubscribeAsync((@event, _) => HandleAgentStream(@event));

        var currentTeamStuff = currentTeam.Stuff;

        //core team
        await PopulateTeam(currentTeamStuff, CoreAgentPool, currentTeam.Name);

        maxQueueCapacityMultiplier = intervals.MaximumQueueCapacityMultiplier;
        maxQueueCapacity = CoreAgentPool.Values.Select(x => x.Capacity).Sum() * maxQueueCapacityMultiplier * intervals.MaximumConcurrency;

        //overflow team
        var overflowTeam = teamsConfig.OverflowTeam;
        await PopulateTeam(overflowTeam.Stuff, OverflowAgentPool, overflowTeam.Name);

        AgentsPool = CombineAgentPools();

        var currentTime = timeProvider.Now().TimeOfDay;

        // TODO: fix boundaries so that next team is allocated if one minute is left for current team. (Maxim Meshkov 2023-10-09)
        await this.RegisterOrUpdateReminder(TeamShiftReminderName, currentTeam.EndWork - currentTime,
            SolutionConst.ReminderPeriod);
    }

    private async Task HandleAgentStream(object @event)
    {
        if (@event is not AgentEvent _)
            //ignore
            return;

        switch (@event)
        {
            case AgentStatusChanged statusChangeEvent:
                await ChangeAgentStatus(statusChangeEvent.AgentId, statusChangeEvent.Status);
                break;
        }
    }

    private Team AllocateCurrentTeam()
    {
        var currentTime = timeProvider.Now().TimeOfDay;

        var availableTeamsBasedOnShift = teamsConfig.CoreTeams
            .Where(x => SolutionHelper.IsTimeInRange(currentTime, x.StartWork, x.EndWork)).ToList();
        var chosenTeam = availableTeamsBasedOnShift.FirstOrDefault();

        if (chosenTeam == null)
        {
            var overallTimeFrames = teamsConfig.CoreTeams.Select(x => (x.Name, x.StartWork, x.EndWork));
            logger.LogError(
                "Error in a process of agent manager initialization - there is not matching team based on current time frame." +
                "Current time: {CurrentTime}; Overall time frames: {OverallTimeFrames}", currentTime,
                overallTimeFrames);
            return null;
        }

        if (availableTeamsBasedOnShift.Count > 1)
        {
            var overlappedTeams = availableTeamsBasedOnShift.Where(x => x.Name != chosenTeam.Name);
            logger.LogWarning(
                "Overlapping team hours detected. Current team would be chosen randomly. Chosen team: {ChosenTeam}; Overlapped teams: {OverlappedTeams}",
                chosenTeam, overlappedTeams);
        }

        return chosenTeam;
    }

    private async Task PopulateTeam(Dictionary<string, int> requestedStuff, Dictionary<int, List<Agent>> agentPool,
        string teamName)
    {
        var seniorityDescriptions = teamsConfig.SeniorityDescriptions;

        for (var i = 0; i < requestedStuff.Count; i++)
        {
            var (senioritySystemName, membersCount) = requestedStuff.ElementAt(i);

            var seniorityDescription = seniorityDescriptions.FirstOrDefault(x => x.Name == senioritySystemName);
            if (seniorityDescription == null)
            {
                logger.LogError(
                    "Team config was wrongly populated, since it has no matching seniority description for given system name: {SystemName}." +
                    "Priority can not be set correctly." +
                    "Agents for this entry would not be created.",
                    senioritySystemName);
                continue;
            }

            for (var j = 0; j < membersCount; j++)
            {
                var agentId =
                    SolutionHelper.AgentIdFormatter(this.GetPrimaryKeyString(), teamName, senioritySystemName, j);
                var maximumConcurrency = intervals.MaximumConcurrency;
                var agentCapacity = seniorityDescriptions.First(x => x.Name == senioritySystemName).Capacity;
                var agentGrain = GrainFactory.GetGrain<IAgentGrain>(agentId);
                var agentStatus = await agentGrain.GetStatus();

                var ret = agentPool.GetOrAdd(seniorityDescription.Priority,
                    _ => new List<Agent>());
                ret.Add(new Agent(agentId, senioritySystemName, seniorityDescription.Priority, agentStatus,
                    agentCapacity, (int)Math.Floor(agentCapacity * maximumConcurrency)));
            }
        }
    }

    private List<Agent> CombineAgentPools() => OverflowAgentPool.Values.SelectMany(x => x)
        .Concat(CoreAgentPool.Values.SelectMany(x => x)).ToList();

    public async Task<Agent> AssignAgent(string sessionId)
    {
        var agent = await AssignFromPool(CoreAgentPool, CorePriorityRoundRobinMap) ??
                    await AssignFromPool(OverflowAgentPool, OverflowPriorityRoundRobinMap);

        return agent;

        async Task<Agent> AssignFromPool(Dictionary<int, List<Agent>> pool, Dictionary<int, string> roundRobinMap)
        {
            foreach (var priority in pool.Keys)
            {
                var availableAgents = pool[priority].Any(x => x.Availability == AgentStatus.Free);

                if (!availableAgents)
                    continue;

                roundRobinMap.TryGetValue(priority, out var lastAllocatedAgentId);

                var idx = FindAgentRoundRobin(priority, lastAllocatedAgentId);

                var assignedAgent = pool[priority][idx];

                var updatedAgentStatus =
                    await GrainFactory.GetGrain<IAgentGrain>(assignedAgent.Id).AssignSession(sessionId);

                assignedAgent.Availability = updatedAgentStatus;

                roundRobinMap[priority] = assignedAgent.Id;

                return await EnrichAgent(assignedAgent);
            }

            //no available agents found
            return null;

            int FindAgentRoundRobin(int priority, string lastAllocatedAgentId)
            {
                var idx = 0;
                if (lastAllocatedAgentId != null)
                {
                    while (pool[priority][idx].Id != lastAllocatedAgentId)
                        idx++;
                    idx = (idx + 1) % pool[priority].Count;
                }

                while (pool[priority][idx].Availability != AgentStatus.Free)
                    idx = (idx + 1) % pool[priority].Count;
                return idx;
            }
        }
    }

    public Task<double> GetMaxQueueCapacity() => Task.FromResult(maxQueueCapacity);

    private async Task ChangeAgentStatus(string agentId, AgentStatus status)
    {
        var agent = AgentsPool.FirstOrDefault(x => x.Id == agentId);

        if (agent == null)
        {
            logger.LogWarning(
                "Can not update agent status, because requested id can not be found. Requested agent id: {AgentId}",
                agentId);
            return;
        }

        agent.Availability = status;

        var allBusy = AgentsPool.All(x => x.Availability == AgentStatus.Busy);

        if (!allBusy)
        {
            var stream = this.GetStream(this.GetPrimaryKeyString(), SolutionConst.QueueManagerStreamNamespace);
            await stream.OnNextAsync(new AllocatePendingSessionEvent());
        }
    }

    public Task<string> GetCurrentTeamName() => Task.FromResult(currentTeamName);

    public async Task<ImmutableList<Agent>> GetCoreTeam() =>
        await EnrichAgents(CoreAgentPool.Values.SelectMany(x => x).Select(x => x).ToImmutableList());

    public async Task<ImmutableList<Agent>> GetOverflowTeam() =>
        await EnrichAgents(OverflowAgentPool.Values.SelectMany(x => x).Select(x => x).ToImmutableList());

    private async Task<ImmutableList<Agent>> EnrichAgents(ImmutableList<Agent> agents) =>
        (await Task.WhenAll(agents.Select(EnrichAgent))).ToImmutableList();

    private async Task<Agent> EnrichAgent(Agent agent)
    {
        var agentGrain = GrainFactory.GetGrain<IAgentGrain>(agent.Id);
        var runningSessions = await agentGrain.GetCurrentSessionIds();
        var currentWorkLoad = await agentGrain.GetCurrentWorkload();

        return agent with { Workload = currentWorkLoad, RunningSessions = runningSessions };
    }

    public async Task ForceShift()
    {
        await ForceShiftInner();
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == TeamShiftReminderName)
            await ForceShiftInner();
    }

    private async Task ForceShiftInner()
    {
        var prevAgentIds = CoreAgentPool.Values.SelectMany(x => x).Select(x => x.Id);

        var nextTeam = AllocateCurrentTeam();
        if (nextTeam == null)
            return;

        currentTeamName = nextTeam.Name;
        await PopulateTeam(nextTeam.Stuff, CoreAgentPool, nextTeam.Name);

        maxQueueCapacity = CoreAgentPool.Values.Select(x => x.Count).Sum() * maxQueueCapacityMultiplier;

        CorePriorityRoundRobinMap.Clear();
        AgentsPool = CombineAgentPools();

        foreach (var prevAgentId in prevAgentIds)
        {
            var prevAgentGrain = GrainFactory.GetGrain<IAgentGrain>(prevAgentId);
            await prevAgentGrain.CloseAgent();
        }

        var stream = this.GetStream(this.GetPrimaryKeyString(), SolutionConst.QueueManagerStreamNamespace);
        await stream.OnNextAsync(new AllocatePendingSessionEvent());

        var currentTime = timeProvider.Now().TimeOfDay;
        await this.RegisterOrUpdateReminder(TeamShiftReminderName, nextTeam.EndWork - currentTime,
            SolutionConst.ReminderPeriod);
    }
}