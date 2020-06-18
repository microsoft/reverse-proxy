// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    internal static class HttpContextFeaturesExtensions
    {
        public static ClusterInfo GetRequiredCluster(this HttpContext context)
        {
            return context.Features.Get<ClusterInfo>() ?? throw new InvalidOperationException("Cluster unspecified.");
        }

        public static IAvailableDestinationsFeature GetRequiredDestinationFeature(this HttpContext context)
        {
            var destinationsFeature = context.Features.Get<IAvailableDestinationsFeature>();
            if (destinationsFeature?.Destinations == null)
            {
                throw new InvalidOperationException("The IAvailableDestinationsFeature Destinations collection was not set.");
            }
            return destinationsFeature;
        }
    }
}
