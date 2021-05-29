// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Yarp.ReverseProxy.Discovery;
using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.Model.Transforms;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding response header and trailer transforms.
    /// </summary>
    public static class ResponseTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will enable or suppress copying response headers to the client response.
        /// </summary>
        public static RouteConfig WithTransformCopyResponseHeaders(this RouteConfig route, bool copy = true)
        {
            return route.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseHeadersCopyKey] = copy ? bool.TrueString : bool.FalseString;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will enable or suppress copying response trailers to the client response.
        /// </summary>
        public static RouteConfig WithTransformCopyResponseTrailers(this RouteConfig route, bool copy = true)
        {
            return route.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseTrailersCopyKey] = copy ? bool.TrueString : bool.FalseString;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will append or set the response header.
        /// </summary>
        public static RouteConfig WithTransformResponseHeader(this RouteConfig route, string headerName, string value, bool append = true, bool always = false)
        {
            var type = append ? ResponseTransformFactory.AppendKey : ResponseTransformFactory.SetKey;
            var when = always ? ResponseTransformFactory.AlwaysValue : ResponseTransformFactory.SuccessValue;
            return route.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseHeaderKey] = headerName;
                transform[type] = value;
                transform[ResponseTransformFactory.WhenKey] = when;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will remove the response header.
        /// </summary>
        public static RouteConfig WithTransformResponseHeaderRemove(this RouteConfig route, string headerName, bool always = false)
        {
            var when = always ? ResponseTransformFactory.AlwaysValue : ResponseTransformFactory.SuccessValue;
            return route.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseHeaderRemoveKey] = headerName;
                transform[ResponseTransformFactory.WhenKey] = when;
            });
        }

        /// <summary>
        /// Adds the transform which will append or set the response header.
        /// </summary>
        public static TransformBuilderContext AddResponseHeader(this TransformBuilderContext context, string headerName, string value, bool append = true, bool always = false)
        {
            context.ResponseTransforms.Add(new ResponseHeaderValueTransform(headerName, value, append, always));
            return context;
        }

        /// <summary>
        /// Adds the transform which will remove the response header.
        /// </summary>
        public static TransformBuilderContext AddResponseHeaderRemove(this TransformBuilderContext context, string headerName, bool always = false)
        {
            context.ResponseTransforms.Add(new ResponseHeaderRemoveTransform(headerName, always));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will append or set the response trailer.
        /// </summary>
        public static RouteConfig WithTransformResponseTrailer(this RouteConfig route, string headerName, string value, bool append = true, bool always = false)
        {
            var type = append ? ResponseTransformFactory.AppendKey : ResponseTransformFactory.SetKey;
            var when = always ? ResponseTransformFactory.AlwaysValue : ResponseTransformFactory.SuccessValue;
            return route.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseTrailerKey] = headerName;
                transform[type] = value;
                transform[ResponseTransformFactory.WhenKey] = when;
            });
        }

        /// <summary>
        /// Adds the transform which will append or set the response trailer.
        /// </summary>
        public static TransformBuilderContext AddResponseTrailer(this TransformBuilderContext context, string headerName, string value, bool append = true, bool always = false)
        {
            context.ResponseTrailersTransforms.Add(new ResponseTrailerValueTransform(headerName, value, append, always));
            return context;
        }

        /// <summary>
        /// Adds the transform which will remove the response trailer.
        /// </summary>
        public static TransformBuilderContext AddResponseTrailerRemove(this TransformBuilderContext context, string headerName, bool always = false)
        {
            context.ResponseTrailersTransforms.Add(new ResponseTrailerRemoveTransform(headerName, always));
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will remove the response trailer.
        /// </summary>
        public static RouteConfig WithTransformResponseTrailerRemove(this RouteConfig route, string headerName, bool always = false)
        {
            var when = always ? ResponseTransformFactory.AlwaysValue : ResponseTransformFactory.SuccessValue;
            return route.WithTransform(transform =>
            {
                transform[ResponseTransformFactory.ResponseTrailerRemoveKey] = headerName;
                transform[ResponseTransformFactory.WhenKey] = when;
            });
        }
    }
}
