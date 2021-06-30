// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace Yarp.ReverseProxy.Sample
{
    internal class MyTransformFactory : ITransformFactory
    {
        public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue("CustomTransform", out var value))
            {
                if (string.IsNullOrEmpty(value))
                {
                    context.Errors.Add(new ArgumentException("A non-empty CustomTransform value is required"));
                }

                return true; // Matched
            }
            return false;
        }

        public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
        {
            if (transformValues.TryGetValue("CustomTransform", out var value))
            {
                if (string.IsNullOrEmpty(value))
                {
                    throw new ArgumentException("A non-empty CustomTransform value is required");
                }

                context.AddRequestTransform(transformContext =>
                {
#if NET
                    transformContext.ProxyRequest.Options.Set(new HttpRequestOptionsKey<string>("CustomTransform"), value);
#else
                    transformContext.ProxyRequest.Properties["CustomTransform"] = value;
#endif
                    return default;
                });

                return true;
            }

            return false;
        }
    }
}
