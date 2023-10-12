using System;

namespace HelpDesk.Core.Extensions;

public class SolutionConst
{
    public static TimeSpan ReminderPeriod = TimeSpan.Parse("00:01:30");

    public const string StreamProviderName = "HelpDesk";
    public const string SessionStreamNamespace = "SessionRoom";
    public const string AgentStreamNamespace = "AgentRoom";
    public const string AgentManagerStreamNamespace = "AgentManagerRoom";
    public const string QueueManagerStreamNamespace = "QueueManagerRoom";

    public const string PrimaryKeySeparator = ".";
    public const string HelpDeskStore = "HelpDeskStorage";
}