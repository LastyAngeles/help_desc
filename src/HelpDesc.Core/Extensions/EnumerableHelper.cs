using System.Collections.Concurrent;
using System.Collections.Generic;
using System;

namespace HelpDesc.Core.Extensions;

public static class EnumerableHelper
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
}