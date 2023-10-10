using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Linq;
using Orleans;
using Orleans.Runtime;
using Orleans.Streams;

namespace HelpDesc.Core.Extensions;

public static class SolutionHelper
{
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key,
        Func<TKey, TValue> factory)
    {
        switch (dictionary)
        {
            case null:
                throw new ArgumentNullException(nameof(dictionary));
            case ConcurrentDictionary<TKey, TValue> concurrentDictionary:
                return concurrentDictionary.GetOrAdd(key, factory);
        }

        if (dictionary.TryGetValue(key, out var value))
            return value;

        value = factory(key);
        dictionary.Add(key, value);
        return value;
    }

    /// <param name="primaryKey">grainId + teamName + seniority system name + idx</param>
    /// <returns>seniority systemName</returns>
    public static string GetAgentSeniority(string primaryKey) => ParsePrimaryInner(primaryKey, 2);

    /// <param name="primaryKey">grainId + teamName + seniority system name + idx</param>
    /// <returns>agent manager grainId</returns>
    public static string GetGrainIdFromAgentPrimary(string primaryKey) => ParsePrimaryInner(primaryKey, 0);

    private static string ParsePrimaryInner(string primaryKey, int skip)
    {
        var splitValue = primaryKey?.Split(SolutionConst.PrimaryKeySeparator);
        return splitValue?.Skip(skip).FirstOrDefault() ?? splitValue?.FirstOrDefault() ?? primaryKey;
    }

    public static string AgentIdFormatter(string grainId, string teamName, string seniority, int idx) =>
        $"{grainId}{SolutionConst.PrimaryKeySeparator}" +
        $"{teamName}{SolutionConst.PrimaryKeySeparator}" +
        $"{seniority}{SolutionConst.PrimaryKeySeparator}" +
        $"{idx}";

    public static bool IsTimeInRange(TimeSpan time, TimeSpan start, TimeSpan end)
    {
        if (start <= end)
        {
            return time >= start && time <= end;
        }

        // Handle the case where the range spans midnight
        return time >= start || time <= end;
    }

    public static IAsyncStream<object> GetStream(this Grain grain, string id, string streamNamespace)
    {
        var sp = grain.GetStreamProvider(SolutionConst.StreamProviderName);
        var streamId = StreamId.Create(streamNamespace, id);
        return sp.GetStream<object>(streamId);
    }
}