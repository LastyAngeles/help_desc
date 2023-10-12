using System;

namespace HelpDesk.Core.Service;

public interface ITimeProvider
{
    DateTime Now();
}

public class TimeProvider : ITimeProvider
{
    public DateTime Now()
    {
        return DateTime.Now;
    }
}