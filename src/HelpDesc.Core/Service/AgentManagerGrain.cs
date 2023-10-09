using System;
using System.Collections.Generic;
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

namespace HelpDesc.Core.Service;

public class AgentManagerGrain : Grain, IAgentManagerGrain, IRemindable
{
    private readonly ITimeProvider timeProvider;
    private readonly ILogger<AgentManagerGrain> logger;
    private readonly TeamsConfig teamsConfig;

    //priority => agent
    private Dictionary<int, List<Agent>> CoreAgentPool { get; } = new();
    private Dictionary<int, List<Agent>> OverflowAgentPool { get; } = new();

    //all agents together
    private List<Agent> AgentsPool { get; set; }

    //priority => last allocated id (for round robin)
    private Dictionary<int, string> CorePriorityRoundRobinMap { get; } = new();
    private Dictionary<int, string> OverflowPriorityRoundRobinMap { get; } = new();

    private double maxQueueCapacity;
    private double maxQueueCapacityMultiplier;

    private const string TeamShiftReminderName = "teamShift";

    public AgentManagerGrain(IOptions<TeamsConfig> teamConfigOptions, ITimeProvider timeProvider,
        ILogger<AgentManagerGrain> logger)
    {
        this.timeProvider = timeProvider;

        this.logger = logger;
        teamsConfig = teamConfigOptions.Value;
    }

    public override async Task OnActivateAsync(CancellationToken cancellationToken)
    {
        await base.OnActivateAsync(cancellationToken);

        var currentTeam = AllocateCurrentTeam();
        if (currentTeam == null)
            return;

        var currentTeamStuff = currentTeam.Stuff;

        //core team
        await PopulateTeam(currentTeamStuff, CoreAgentPool, currentTeam.Name);

        maxQueueCapacityMultiplier = teamsConfig.MaximumQueueCapacityMultiplier;
        maxQueueCapacity = CoreAgentPool.Values.Select(x => x.Count).Sum() * maxQueueCapacityMultiplier;

        //overflow team
        var overflowTeam = teamsConfig.OverflowTeam;
        await PopulateTeam(overflowTeam.Stuff, OverflowAgentPool, overflowTeam.Name);

        AgentsPool = CombineAgentPools();

        var currentTime = timeProvider.Now().TimeOfDay;

        // TODO: fix boundaries so that next team is allocated if one minute is left for current team. (Maxim Meshkov 2023-10-09)
        await this.RegisterOrUpdateReminder(TeamShiftReminderName, currentTeam.EndWork - currentTime,
            TimeSpan.Zero);
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
                var agentId = SolutionHelper.AgentIdFormatter(teamName, senioritySystemName, j);
                var agentGrain = GrainFactory.GetGrain<IAgentGrain>(agentId);
                var agentStatus = await agentGrain.GetStatus();

                var ret = agentPool.GetOrAdd(seniorityDescription.Priority,
                    _ => new List<Agent>());
                ret.Add(new Agent(agentId, senioritySystemName, seniorityDescription.Priority, agentStatus));
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
                var availableAgents = pool[priority].Where(x => x.Availability == Status.Free).ToList();

                if (!availableAgents.Any())
                    continue;

                roundRobinMap.TryGetValue(priority, out var lastAllocatedAgentId);

                var assignedAgent = availableAgents.Count == 1
                    ? availableAgents.Single()
                    : availableAgents.FirstOrDefault(x => x.Id != lastAllocatedAgentId) ?? availableAgents.First();

                var updatedAgentStatus =
                    await GrainFactory.GetGrain<IAgentGrain>(assignedAgent.Id).AssignSession(sessionId);

                assignedAgent.Availability = updatedAgentStatus;

                roundRobinMap[priority] = assignedAgent.Id;

                return assignedAgent;
            }

            //no available agents found
            return null;
        }
    }

    public Task<double> GetMaxQueueCapacity() => Task.FromResult(maxQueueCapacity);

    public async Task ChangeAgentStatus(string agentId, Status status)
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

        var allBusy = AgentsPool.All(x => x.Availability == Status.Busy);

        if (!allBusy)
        {
            var queueManager = GrainFactory.GetGrain<IQueueManagerGrain>(0);
            var sessionId = await queueManager.AllocateSinglePendingSession();
            if (sessionId != null)
                await AssignAgent(sessionId);
        }
    }

    public async Task ReceiveReminder(string reminderName, TickStatus status)
    {
        if (reminderName == TeamShiftReminderName)
        {
            var prevAgentIds = CoreAgentPool.Values.SelectMany(x => x).Select(x => x.Id);

            var nextTeam = AllocateCurrentTeam();
            await PopulateTeam(nextTeam.Stuff, CoreAgentPool, nextTeam.Name);
            CorePriorityRoundRobinMap.Clear();
            AgentsPool = CombineAgentPools();

            foreach (var prevAgentId in prevAgentIds)
            {
                var prevAgentGrain = GrainFactory.GetGrain<IAgentGrain>(prevAgentId);
                await prevAgentGrain.CloseAgent();
            }

            var queueManagerGrain = GrainFactory.GetGrain<IQueueManagerGrain>(0);
            var sessionIds = await queueManagerGrain.AllocatePendingSessions();

            foreach (var sessionId in sessionIds)
            {
                var agent = await AssignAgent(sessionId);
                if (agent == null)
                    break;

                sessionIds.Remove(sessionId);
            }

            if (sessionIds.Any())
            {
                // TODO: means, that next team can not handle queue length from prev. team (Maxim Meshkov 2023-10-09)
                // TODO: find better way for such scenario (Maxim Meshkov 2023-10-09)
                logger.LogError("Fresh team {TeamName} can not hold the queue from previous team.", nextTeam.Name);
            }

            var currentTime = timeProvider.Now().TimeOfDay;
            await this.RegisterOrUpdateReminder(TeamShiftReminderName, nextTeam.EndWork - currentTime, TimeSpan.Zero);
        }
    }
}