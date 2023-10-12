using System;
using HelpDesk.Core.Service;

namespace HelpDesk.Core.Test;

public class TestTimeProvider : ITimeProvider
{
    private DateTime? now;

    public DateTime Now()
    {
        return now ?? DateTime.Now;
    }

    public void SetNow(DateTime? now)
    {
        this.now = now;
    }
}