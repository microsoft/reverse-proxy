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
        public void Apply(TransformBuilderContext transformBuildContext)
        {
            // Check all routes for a custom property and add the associated transform.
            string value = null;
            if (transformBuildContext.Route.Metadata?.TryGetValue("CustomMetadata", out value) ?? false)
            {
                transformBuildContext.AddRequestTransform(transformContext =>
                {
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new ArgumentException("A non-empty CustomTransform value is required");
                    }

#if NET
                    transformContext.ProxyRequest.Options.Set(new HttpRequestOptionsKey<string>("CustomMetadata"), value);
#else
                    transformContext.ProxyRequest.Properties["CustomMetadata"] = value;
#endif
                    return Task.CompletedTask;
                });
            }
        }
    }
}
