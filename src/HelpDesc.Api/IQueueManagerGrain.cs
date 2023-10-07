﻿using System.Threading.Tasks;
using HelpDesc.Api.Model;
using Orleans;

namespace HelpDesc.Api;

public interface IQueueManagerGrain : IGrainWithIntegerKey
{
    Task<SessionCreateResult> CreateSession();
}