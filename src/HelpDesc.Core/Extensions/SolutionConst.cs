using System;

namespace HelpDesc.Core.Extensions;

public class SolutionConst
{
    public static TimeSpan ReminderPeriod = TimeSpan.Parse("00:01:30");

    public const string StreamProviderName = "HelpDesc";
    public const string SessionStreamNamespace = "SessionRoom";
    public const string AgentStreamNamespace = "AgentRoom";

    public const string PrimaryKeySeparator = ".";
    public const string HelpDescStore = "HelpDescStorage";
}