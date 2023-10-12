# help desk

## Task overview

1. **Initiating Support Request**: A user initiates a support request through an API endpoint that creates and queues a chat session. The session is placed in a First-In-First-Out (FIFO) queue and monitored. If the session queue is full, chat requests are refused, except during office hours with available overflow capacity.

2. **Session Monitoring**: Once a chat session is created, it begins polling every 1 second after receiving an "OK" response. A monitoring system marks a session as inactive if it hasn't received 3 poll requests.

3. **Agent Assignment and Queue Rules**:
   - Agents work in 3-shift rotations, each lasting 8 hours.
   - At the end of a shift, agents complete their current chats but are not assigned new ones.
   - The chat capacity is determined by the number of agents available, multiplied by their seniority, and rounded down.
   - The maximum allowed queue length is the team's capacity multiplied by 1.5.

4. **Overflow Handling**: If the maximum queue length is reached during office hours, an overflow team is activated. This team, consisting of individuals not typically handling these tasks, is considered to have the efficiency of a junior agent. The maximum number of chats an agent can handle simultaneously is 10, with capacity adjustments based on seniority.

## Teams and Assignment

**Available Teams**:
- Team A: 1 Team Lead, 2 Mid-Level, 1 Junior
- Team B: 1 Senior, 1 Mid-Level, 2 Juniors
- Team C: 2 Mid-Level (night shift team)
- Overflow team: 6 individuals considered as Junior.

**Chat Assignment**: Chats are assigned in a round-robin fashion, with a preference for assigning juniors first, followed by mid-level agents, seniors, and so on. This approach ensures that higher-seniority agents are more available to assist lower-seniority agents.

*Example Scenarios*:
- A team of 2 people: 1 Senior (capacity 8), 1 Junior (capacity 4). If 5 chats arrive, 4 would be assigned to the Junior and 1 to the Senior.
- A team of 2 Juniors and 1 Mid-Level: If 6 chats arrive, 3 would be assigned to each Junior, and none to the Mid-Level agent.

## Start Application

### Prerequisites:
* docker

* .net 7 SDK

docker:
1. Navigate to help_desk\scripts folder and execute `docker-build.cmd`
2. Execute `docker compose up`
3. Navigate to localhost:8765/swagger for host main API

_in order to be able to debug with docker setup_
1. attach to process with connection type `docker`
2. choose connection target `scripts-help-desk-core-1` or `scripts-help-desk-host-1`

tye:
1. Execute `dotnet tool restore`
2. Navigate to help_desk\scripts folder and execute `dotnet tye run`
3. Navigate to localhost:8765/swagger for host main API
   
_in order to be able to debug with tye setup_
1. attach to process with connection type `default`
2. choose process to attach `HelpDesc.Host` or `HelpDesc.Core`

### Notes:

1. timezone can be different in case you choose docker startup, so team allocation would be based on "docker timezone"