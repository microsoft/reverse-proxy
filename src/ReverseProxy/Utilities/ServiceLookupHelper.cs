// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Utilities
{
    public static class ServiceLookupHelper
    {
        internal static IDictionary<string, T> ToDictionaryByUniqueId<T>(this IEnumerable<T> services, Func<T, string> idSelector)
        {
            if (services == null)
            {
                throw new ArgumentNullException(nameof(services));
            }

            var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);

            foreach (var service in services)
            {
                if (!result.TryAdd(idSelector(service), service))
                {
                    throw new ArgumentException(nameof(services), $"More than one {typeof(T)} found with the same identifier.");
                }
            }

            return result;
        }

        internal static T GetRequiredServiceById<T>(this IDictionary<string, T> services, string id)
        {
            return services.GetRequiredServiceById<T>(id, id);
        }

        internal static T GetRequiredServiceById<T>(this IDictionary<string, T> services, string id, string defaultId)
        {
            var lookup = id;
            if (string.IsNullOrEmpty(lookup))
            {
                lookup = defaultId;
            }

            if (!services.TryGetValue(lookup, out var result))
            {
                throw new ArgumentException(nameof(id), $"No {typeof(T)} was found for the id {lookup}.");
            }
            return result;
        }
    }
}
