using System;

namespace HelpDesc.Host;

public record OrleansConnection
{
    public int MaxAttempts { get; set; }
    public TimeSpan RetryDelay { get; set; }
}