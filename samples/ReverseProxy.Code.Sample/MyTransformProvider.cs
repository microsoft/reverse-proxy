// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions.Config;

namespace Microsoft.ReverseProxy.Sample
{
    internal class MyTransformProvider : ITransformProvider
    {
        public void ValidateRoute(TransformValidationContext context)
        {
            // Check all routes for a custom property and validate the associated transform data.
            string value = null;
            if (context.Route.Metadata?.TryGetValue("CustomMetadata", out value) ?? false)
            {
                if (string.IsNullOrEmpty(value))
                {
                    context.Errors.Add(new ArgumentException("A non-empty CustomMetadata value is required"));
                }
            }
        }

        public void ValidateCluster(TransformValidationContext context)
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
            string value = null;
            if ((transformBuildContext.Route.Metadata?.TryGetValue("CustomMetadata", out value) ?? false)
                || (transformBuildContext.Cluster.Metadata?.TryGetValue("CustomMetadata", out value) ?? false))
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("A non-empty CustomMetadata value is required");
                }

                transformBuildContext.AddRequestTransform(transformContext =>
                {
#if NET
                    transformContext.ProxyRequest.Options.Set(new HttpRequestOptionsKey<string>("CustomMetadata"), value);
#else
                    transformContext.ProxyRequest.Properties["CustomMetadata"] = value;
#endif
                    return default;
                });
            }
        }
    }
}
