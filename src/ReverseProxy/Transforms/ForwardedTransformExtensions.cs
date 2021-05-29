// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Discovery;
using Yarp.ReverseProxy.Service.Config;
using Yarp.ReverseProxy.Service.RuntimeModel.Transforms;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding forwarded header transforms.
    /// </summary>
    public static class ForwardedTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will add X-Forwarded-* headers.
        /// </summary>
        public static RouteConfig WithTransformXForwarded(this RouteConfig route, string headerPrefix = "X-Forwarded-", bool useFor = true,
            bool useHost = true, bool useProto = true, bool usePrefix = true, bool append = true)
        {
            var headers = new List<string>();

            if (useFor)
            {
                headers.Add(ForwardedTransformFactory.ForKey);
            }

            if (usePrefix)
            {
                headers.Add(ForwardedTransformFactory.PrefixKey);
            }

            if (useHost)
            {
                headers.Add(ForwardedTransformFactory.HostKey);
            }

            if (useProto)
            {
                headers.Add(ForwardedTransformFactory.ProtoKey);
            }

            return route.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.XForwardedKey] = string.Join(',', headers);
                transform[ForwardedTransformFactory.AppendKey] = append.ToString();
                transform[ForwardedTransformFactory.PrefixForwardedKey] = headerPrefix;
            });
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-* request headers.
        /// </summary>
        public static TransformBuilderContext AddXForwarded(this TransformBuilderContext context, string headerPrefix = "X-Forwarded-",
            bool useFor = true, bool useHost = true, bool useProto = true, bool usePrefix = true, bool append = true)
        {
            context.UseDefaultForwarders = false;
            if (useFor)
            {
                context.RequestTransforms.Add(new RequestHeaderXForwardedForTransform(headerPrefix + ForwardedTransformFactory.ForKey, append));
            }
            if (useHost)
            {
                context.RequestTransforms.Add(new RequestHeaderXForwardedHostTransform(headerPrefix + ForwardedTransformFactory.HostKey, append));
            }
            if (useProto)
            {
                context.RequestTransforms.Add(new RequestHeaderXForwardedProtoTransform(headerPrefix + ForwardedTransformFactory.ProtoKey, append));
            }
            if (usePrefix)
            {
                context.RequestTransforms.Add(new RequestHeaderXForwardedPrefixTransform(headerPrefix + ForwardedTransformFactory.PrefixKey, append));
            }
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will add the Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static RouteConfig WithTransformForwarded(this RouteConfig route, bool useHost = true, bool useProto = true,
            NodeFormat forFormat = NodeFormat.Random, NodeFormat byFormat = NodeFormat.Random, bool append = true)
        {
            var headers = new List<string>();

            if (forFormat != NodeFormat.None)
            {
                headers.Add(ForwardedTransformFactory.ForKey);
            }

            if (byFormat != NodeFormat.None)
            {
                headers.Add(ForwardedTransformFactory.ByKey);
            }

            if (useHost)
            {
                headers.Add(ForwardedTransformFactory.HostKey);
            }

            if (useProto)
            {
                headers.Add(ForwardedTransformFactory.ProtoKey);
            }

            return route.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.ForwardedKey] = string.Join(',', headers);
                transform[ForwardedTransformFactory.AppendKey] = append.ToString();

                if (forFormat != NodeFormat.None)
                {
                    transform.Add(ForwardedTransformFactory.ForFormatKey, forFormat.ToString());
                }

                if (byFormat != NodeFormat.None)
                {
                    transform.Add(ForwardedTransformFactory.ByFormatKey, byFormat.ToString());
                }
            });
        }

        /// <summary>
        /// Adds the transform which will add the Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static TransformBuilderContext AddForwarded(this TransformBuilderContext context,
            bool useHost = true, bool useProto = true, NodeFormat forFormat = NodeFormat.Random,
            NodeFormat byFormat = NodeFormat.Random, bool append = true)
        {
            context.UseDefaultForwarders = false;
            if (byFormat != NodeFormat.None || forFormat != NodeFormat.None || useHost || useProto)
            {
                var random = context.Services.GetRequiredService<IRandomFactory>();
                context.RequestTransforms.Add(new RequestHeaderForwardedTransform(random,
                    forFormat, byFormat, useHost, useProto, append));
            }
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will set the given header with the Base64 encoded client certificate.
        /// </summary>
        public static RouteConfig WithTransformClientCertHeader(this RouteConfig route, string headerName)
        {
            return route.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.ClientCertKey] = headerName;
            });
        }

        /// <summary>
        /// Adds the transform which will set the given header with the Base64 encoded client certificate.
        /// </summary>
        public static TransformBuilderContext AddClientCertHeader(this TransformBuilderContext context, string headerName)
        {
            context.RequestTransforms.Add(new RequestHeaderClientCertTransform(headerName));
            return context;
        }
    }
}
