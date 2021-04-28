// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding transforms to ProxyRoute.
    /// </summary>
    public static class ProxyRouteTransformExtensions
    {
        /// <summary>
        /// Clones the ProxyRoute and adds the transform.
        /// </summary>
        /// <returns>The cloned route with the new transform.</returns>
        public static RouteConfig WithTransform(this RouteConfig route, Action<IDictionary<string, string>> createTransform)
        {
            if (createTransform is null)
            {
                throw new ArgumentNullException(nameof(createTransform));
            }

            List<IReadOnlyDictionary<string, string>> transforms;
            if (route.Transforms == null)
            {
                transforms = new List<IReadOnlyDictionary<string, string>>();
            }
            else
            {
                transforms = new List<IReadOnlyDictionary<string, string>>(route.Transforms.Count + 1);
                transforms.AddRange(route.Transforms);
            }

            var transform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            createTransform(transform);
            transforms.Add(transform);

            return route with { Transforms = transforms };
        }
    }
}
