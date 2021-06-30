// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Utilities
{
    internal static class CaseSensitiveEqualHelper
    {
        internal static bool Equals(IReadOnlyList<string>? list1, IReadOnlyList<string>? list2)
        {
            if (ReferenceEquals(list1, list2))
            {
                return true;
            }

            if (list1 == null || list2 == null)
            {
                return false;
            }

            if (list1.Count != list2.Count)
            {
                return false;
            }

            for (var i = 0; i < list1.Count; i++)
            {
                if (!string.Equals(list1[i], list2[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        internal static int GetHashCode(IReadOnlyList<string>? values)
        {
            return values?.GetHashCode() ?? 0;
        }
    }
}
