// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.Sample
{
    internal class MyTransformProvider : ITransformProvider
    {
        public void ValidateRoute(TransformRouteValidationContext context)
        {
            // Check all routes for a custom property and validate the associated transform data.
            if (context.Route.Metadata?.TryGetValue("CustomMetadata", out var value) ?? false)
            {
                if (string.IsNullOrEmpty(value))
                {
                    context.Errors.Add(new ArgumentException("A non-empty CustomMetadata value is required"));
                }
            }
        }

        public void ValidateCluster(TransformClusterValidationContext context)
        {
            // Check all clusters for a custom property and validate the associated transform data.
            string value = null;
            if (context.Cluster.Metadata?.TryGetValue("CustomMetadata", out value) ?? false)
            {
                if (string.IsNullOrEmpty(value))
                {
                    context.Errors.Add(new ArgumentException("A non-empty CustomMetadata value is required"));
                }
            }
        }

        public void Apply(TransformBuilderContext transformBuildContext)
        {
            // Check all routes for a custom property and add the associated transform.
            if ((transformBuildContext.Route.Metadata?.TryGetValue("CustomMetadata", out var value) ?? false)
                || (transformBuildContext.Cluster?.Metadata?.TryGetValue("CustomMetadata", out value) ?? false))
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("A non-empty CustomMetadata value is required");
                }

                transformBuildContext.AddRequestTransform(transformContext =>
                {
                    transformContext.ProxyRequest.Options.Set(new HttpRequestOptionsKey<string>("CustomMetadata"), value);
                    return default;
                });
            }
        }
    }
}
