// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Utilities
{
    internal class CaseInsensitiveEqualHelper
    {
        internal static bool Equals(IReadOnlyList<string> list1, IReadOnlyList<string> list2)
        {
            if (ReferenceEquals(list1, list2))
            {
                return true;
            }

            if ((list1?.Count ?? 0) == 0 && (list2?.Count ?? 0) == 0)
            {
                return true;
            }

            if (list1 != null && list2 == null || list1 == null && list2 != null)
            {
                return false;
            }

            for (var i = 0; i < list1.Count; i++)
            {
                if (!string.Equals(list1[i], list2[i], StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool Equals(IList<IDictionary<string, string>> dictionaryList1, IList<IDictionary<string, string>> dictionaryList2)
        {
            if (ReferenceEquals(dictionaryList1, dictionaryList2))
            {
                return true;
            }

            if ((dictionaryList1?.Count ?? 0) == 0 && (dictionaryList2?.Count ?? 0) == 0)
            {
                return true;
            }

            if (dictionaryList1 != null && dictionaryList2 == null || dictionaryList1 == null && dictionaryList2 != null)
            {
                return false;
            }

            for (var i = 0; i < dictionaryList1.Count; i++)
            {
                if (!Equals(dictionaryList1[i], dictionaryList2[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal static bool Equals(IDictionary<string, string> dictionary1, IDictionary<string, string> dictionary2)
        {
            return Equals(dictionary1, dictionary2, StringEquals);
        }

        private static bool StringEquals(string value1, string value2)
        {
            return string.Equals(value1, value2, StringComparison.OrdinalIgnoreCase);
        }

        internal static bool Equals<T>(IDictionary<string, T> dictionary1, IDictionary<string, T> dictionary2, Func<T, T, bool> comparer)
        {
            if (ReferenceEquals(dictionary1, dictionary2))
            {
                return true;
            }

            if ((dictionary1?.Count ?? 0) == 0 && (dictionary2?.Count ?? 0) == 0)
            {
                return true;
            }

            if (dictionary1 != null && dictionary2 == null || dictionary1 == null && dictionary2 != null)
            {
                return false;
            }

            foreach (var (key, value1) in dictionary1)
            {
                if (dictionary2.TryGetValue(key, out var value2))
                {
                    if (!comparer(value1, value2))
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
    }
}
