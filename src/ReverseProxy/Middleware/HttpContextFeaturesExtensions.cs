// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Middleware
{
    internal static class HttpContextFeaturesExtensions
    {
        public static BackendInfo GetRequiredBacked(this HttpContext context)
        {
            return context.Features.Get<BackendInfo>() ?? throw new InvalidOperationException("Backend unspecified.");
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
