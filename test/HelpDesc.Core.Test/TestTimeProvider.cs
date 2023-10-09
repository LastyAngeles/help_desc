using System;
using HelpDesc.Core.Service;
using Xunit;

namespace HelpDesc.Core.Test;

public class TestTimeProvider : ITimeProvider
{
    private DateTime? now;

    public DateTime Now()
    {
        return now.HasValue ? now.Value : DateTime.Now;
    }

    public void SetNow(DateTime? now)
    {
        this.now = now; 
    }
}
