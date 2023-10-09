using System.Collections.Concurrent;
using System.Collections.Generic;
using System;
using System.Linq;

namespace HelpDesc.Core.Extensions;

public static class SolutionHelper
{
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TKey, TValue> factory)
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

    /// <param name="primaryKey">TeamName + seniority system name + idx</param>
    /// <returns></returns>
    public static string GetAgentSeniority(string primaryKey)
    {
        return primaryKey?.Split(SolutionConst.PrimaryKeySeparator).Skip(1).First();
    }

    public static bool IsTimeInRange(TimeSpan time, TimeSpan start, TimeSpan end)
    {
        if (start <= end)
        {
            return time >= start && time <= end;
        }

        // Handle the case where the range spans midnight
        return time >= start || time <= end;
    }
}