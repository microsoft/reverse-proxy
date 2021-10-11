// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Yarp.ReverseProxy.Utilities
{
    internal static class CollectionEqualityHelper
    {
        public static bool Equals<T>(IReadOnlyList<T>? list1, IReadOnlyList<T>? list2, IEqualityComparer<T>? valueComparer = null)
        {
            if (ReferenceEquals(list1, list2))
            {
                return true;
            }

            if (list1 is null || list2 is null)
            {
                return false;
            }

            if (list1.Count != list2.Count)
            {
                return false;
            }

            valueComparer ??= EqualityComparer<T>.Default;

            for (var i = 0; i < list1.Count; i++)
            {
                if (!valueComparer.Equals(list1[i], list2[i]))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Equals<T>(IReadOnlyDictionary<string, T>? dictionary1, IReadOnlyDictionary<string, T>? dictionary2, IEqualityComparer<T>? valueComparer = null)
        {
            if (ReferenceEquals(dictionary1, dictionary2))
            {
                return true;
            }

            if (dictionary1 is null || dictionary2 is null)
            {
                return false;
            }

            if (dictionary1.Count != dictionary2.Count)
            {
                return false;
            }

            if (dictionary1.Count == 0)
            {
                return true;
            }

            valueComparer ??= EqualityComparer<T>.Default;

            foreach (var (key, value1) in dictionary1)
            {
                if (dictionary2.TryGetValue(key, out var value2))
                {
                    if (!valueComparer.Equals(value1, value2))
                    {
                        return false;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }

        public static bool Equals<T>(IReadOnlyList<IReadOnlyDictionary<string, T>>? dictionaryList1, IReadOnlyList<IReadOnlyDictionary<string, T>>? dictionaryList2, IEqualityComparer<T>? valueComparer = null)
        {
            if (ReferenceEquals(dictionaryList1, dictionaryList2))
            {
                return true;
            }

            if (dictionaryList1 is null || dictionaryList2 is null)
            {
                return false;
            }

            if (dictionaryList1.Count != dictionaryList2.Count)
            {
                return false;
            }

            for (var i = 0; i < dictionaryList1.Count; i++)
            {
                if (!Equals(dictionaryList1[i], dictionaryList2[i], valueComparer))
                {
                    return false;
                }
            }

            return true;
        }

        public static int GetHashCode<T>(IReadOnlyList<T>? values, IEqualityComparer<T>? valueComparer = null)
        {
            if (values is null)
            {
                return 0;
            }

            valueComparer ??= EqualityComparer<T>.Default;

            var hashCode = new HashCode();
            foreach (var value in values)
            {
                hashCode.Add(value, valueComparer);
            }
            return hashCode.ToHashCode();
        }

        public static int GetHashCode<T>(IReadOnlyDictionary<string, T>? dictionary)
        {
            if (dictionary is null)
            {
                return 0;
            }

            // We don't know what comparer the dictionary was created with, so we can't reliably create correct hash codes based on keys
            return dictionary.Count;
        }

        public static int GetHashCode<T>(IReadOnlyList<IReadOnlyDictionary<string, T>>? dictionaryList)
        {
            if (dictionaryList is null)
            {
                return 0;
            }

            var hashCode = new HashCode();
            foreach (var dictionary in dictionaryList)
            {
                hashCode.Add(GetHashCode(dictionary));
            }
            return hashCode.ToHashCode();
        }
    }
}
