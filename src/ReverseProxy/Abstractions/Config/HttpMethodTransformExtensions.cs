// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for modifying the request method.
    /// </summary>
    public static class HttpMethodTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform that will replace the HTTP method if it matches.
        /// </summary>
        public static ProxyRoute WithTransformHttpMethod(this ProxyRoute proxyRoute, string fromHttpMethod, string toHttpMethod)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[HttpMethodTransformFactory.HttpMethodKey] = fromHttpMethod;
                transform[HttpMethodTransformFactory.SetKey] = toHttpMethod;
            });
        }

        /// <summary>
        /// Adds the transform that will replace the HTTP method if it matches.
        /// </summary>
        public static TransformBuilderContext AddHttpMethodChange(this TransformBuilderContext context, string fromHttpMethod, string toHttpMethod)
        {
            context.RequestTransforms.Add(new HttpMethodTransform(fromHttpMethod, toHttpMethod));
            return context;
        }
    }
}
