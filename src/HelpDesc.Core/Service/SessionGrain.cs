﻿using HelpDesc.Api;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Core.Service;

public class SessionGrain : Grain, ISessionGrain
{
    public SessionStatus GetStatus()
    {
        throw new System.NotImplementedException();
    }
}