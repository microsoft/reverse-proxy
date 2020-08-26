// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.ReverseProxy
{
    internal static class DeepCloneableExtensions
    {
        public static T DeepClone<T>(this T item)
            where T : IDeepCloneable<T>
        {
            return item.DeepClone();
        }

        public static IList<T> DeepClone<T>(this IList<T> list)
            where T : IDeepCloneable<T>
        {
            return list.Select(entry => entry.DeepClone()).ToList();
        }

        public static List<string> DeepClone(this IList<string> list)
        {
            return list != null ? new List<string>(list) : null;
        }

        public static IDictionary<string, string> DeepClone(this IDictionary<string, string> dictionary, IEqualityComparer<string> comparer)
        {
            return dictionary.ToDictionary(entry => entry.Key, entry => entry.Value, comparer);
        }

        public static IDictionary<string, TValue> DeepClone<TValue>(this IDictionary<string, TValue> dictionary, IEqualityComparer<string> comparer) where TValue : IDeepCloneable<TValue>
        {
            return dictionary.ToDictionary(entry => entry.Key, entry => entry.Value.DeepClone(), comparer);
        }
    }
}
