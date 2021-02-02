// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding response header and trailer transforms.
    /// </summary>
    public static class ResponseTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will prevent copying response headers to the client response.
        /// </summary>
        public static ProxyRoute WithTransformSuppressResponseHeaders(this ProxyRoute proxyRoute, bool suppress = true)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseHeadersCopyKey] = suppress ? bool.FalseString : bool.TrueString;
            });
        }

        /// <summary>
        /// Adds the transform which will prevent copying response headers to the client response.
        /// </summary>
        public static TransformBuilderContext SuppressResponseHeaders(this TransformBuilderContext context, bool suppress = true)
        {
            context.CopyResponseHeaders = !suppress;
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will prevent copying response trailers to the client response.
        /// </summary>
        public static ProxyRoute WithTransformSuppressResponseTrailers(this ProxyRoute proxyRoute, bool suppress = true)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseTrailersCopyKey] = suppress ? bool.FalseString : bool.TrueString;
            });
        }

        /// <summary>
        /// Adds the transform which will prevent copying response trailers to the client response.
        /// </summary>
        public static TransformBuilderContext SuppressResponseTrailers(this TransformBuilderContext context, bool suppress = true)
        {
            context.CopyResponseTrailers = !suppress;
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will append or set the response header.
        /// </summary>
        public static ProxyRoute WithTransformResponseHeader(this ProxyRoute proxyRoute, string headerName, string value, bool append = true, bool always = true)
        {
            var type = append ? ResponseTransformFactory.AppendKey : ResponseTransformFactory.SetKey;
            var when = always ? ResponseTransformFactory.AlwaysValue : ResponseTransformFactory.SuccessValue;
            return proxyRoute.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseHeaderKey] = headerName;
                transform[type] = value;
                transform[ResponseTransformFactory.WhenKey] = when;
            });
        }

        /// <summary>
        /// Adds the transform which will append or set the response header.
        /// </summary>
        public static TransformBuilderContext AddResponseHeader(this TransformBuilderContext context, string headerName, string value, bool append = true, bool always = true)
        {
            context.ResponseTransforms.Add(new ResponseHeaderValueTransform(headerName, value, append, always));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will append or set the response trailer.
        /// </summary>
        public static ProxyRoute WithTransformResponseTrailer(this ProxyRoute proxyRoute, string headerName, string value, bool append = true, bool always = true)
        {
            var type = append ? ResponseTransformFactory.AppendKey : ResponseTransformFactory.SetKey;
            var when = always ? ResponseTransformFactory.AlwaysValue : ResponseTransformFactory.SuccessValue;
            return proxyRoute.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseTrailerKey] = headerName;
                transform[type] = value;
                transform[ResponseTransformFactory.WhenKey] = when;
            });
        }

        /// <summary>
        /// Adds the transform which will append or set the response trailer.
        /// </summary>
        public static TransformBuilderContext AddResponseTrailer(this TransformBuilderContext context, string headerName, string value, bool append = true, bool always = true)
        {
            context.ResponseTrailersTransforms.Add(new ResponseTrailerValueTransform(headerName, value, append, always));
            return context;
        }
    }
}
