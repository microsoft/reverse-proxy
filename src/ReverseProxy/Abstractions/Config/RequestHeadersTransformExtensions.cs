// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding request header transforms.
    /// </summary>
    public static class RequestHeadersTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will prevent copying request headers to the proxy request.
        /// </summary>
        public static ProxyRoute WithTransformSuppressRequestHeaders(this ProxyRoute proxyRoute)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[RequestHeadersTransformFactory.RequestHeadersCopyKey] = "false";
            });
        }

        /// <summary>
        /// Adds the transform which will prevent copying request headers to the proxy request.
        /// </summary>
        public static TransformBuilderContext AddSuppressRequestHeaders(this TransformBuilderContext context, bool suppress = true)
        {
            context.CopyRequestHeaders = !suppress;
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will copy the incoming request Host header to the proxy request.
        /// </summary>
        public static ProxyRoute WithTransformUseOriginalHostHeader(this ProxyRoute proxyRoute, bool useOriginal = true)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[RequestHeadersTransformFactory.RequestHeaderOriginalHostKey] = useOriginal ? "true" : "false";
            });
        }

        /// <summary>
        /// Adds the transform which will copy the incoming request Host header to the proxy request.
        /// </summary>
        public static TransformBuilderContext AddOriginalHostHeader(this TransformBuilderContext context, bool useOriginal = true)
        {
            context.UseOriginalHost = useOriginal;
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will append or set the request header.
        /// </summary>
        public static ProxyRoute WithTransformRequestHeader(this ProxyRoute proxyRoute, string headerName, string value, bool append = true)
        {
            var type = append ? RequestHeadersTransformFactory.AppendKey : RequestHeadersTransformFactory.SetKey;
            return proxyRoute.WithTransform(transform =>
            {
                transform[RequestHeadersTransformFactory.RequestHeaderKey] = headerName;
                transform[type] = value;
            });
        }

        /// <summary>
        /// Adds the transform which will append or set the request header.
        /// </summary>
        public static TransformBuilderContext AddOriginalHostHeader(this TransformBuilderContext context, string headerName, string value, bool append = true)
        {
            context.RequestTransforms.Add(new RequestHeaderValueTransform(headerName, value, append));
            return context;
        }
    }
}
