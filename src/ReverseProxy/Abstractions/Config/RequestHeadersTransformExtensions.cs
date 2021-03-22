// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding request header transforms.
    /// </summary>
    public static class RequestHeadersTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will enable or suppress copying request headers to the proxy request.
        /// </summary>
        public static ProxyRoute WithTransformCopyRequestHeaders(this ProxyRoute proxyRoute, bool copy = true)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[RequestHeadersTransformFactory.RequestHeadersCopyKey] = copy ? bool.TrueString : bool.FalseString;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will copy the incoming request Host header to the proxy request.
        /// </summary>
        public static ProxyRoute WithTransformUseOriginalHostHeader(this ProxyRoute proxyRoute, bool useOriginal = true)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[RequestHeadersTransformFactory.RequestHeaderOriginalHostKey] = useOriginal ? bool.TrueString : bool.FalseString;
            });
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
        public static TransformBuilderContext AddRequestHeader(this TransformBuilderContext context, string headerName, string value, bool append = true)
        {
            context.RequestTransforms.Add(new RequestHeaderValueTransform(headerName, value, append));
            return context;
        }
    }
}
