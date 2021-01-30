// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.ReverseProxy.Service.Config;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding forwarded header transforms.
    /// </summary>
    public static class ForwardedTransformExtensions
    {
        /// <summary>
        /// Clones the route and adds the transform which will add X-Forwarded-* headers.
        /// </summary>
        public static ProxyRoute WithTransformXForwarded(this ProxyRoute proxyRoute, string headerPrefix = "X-Forwarded-", bool useFor = true, bool useHost = true, bool useProto = true, bool usePathBase = true, bool append = true)
        {
            var headers = new List<string>();

            if (useFor)
            {
                headers.Add(ForwardedTransformFactory.ForKey);
            }

            if (usePathBase)
            {
                headers.Add(ForwardedTransformFactory.PathBaseKey);
            }

            if (useHost)
            {
                headers.Add(ForwardedTransformFactory.HostKey);
            }

            if (useProto)
            {
                headers.Add(ForwardedTransformFactory.ProtoKey);
            }

            return proxyRoute.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.XForwardedKey] = string.Join(',', headers);
                transform[ForwardedTransformFactory.AppendKey] = append.ToString();
                transform[ForwardedTransformFactory.PrefixKey] = headerPrefix;
            });
        }

        /// <summary>
        /// Adds the transform which will add X-Forwarded-* request headers.
        /// </summary>
        public static TransformBuilderContext AddXForwarded(this TransformBuilderContext context, string headerPrefix = "X-Forwarded-",
            bool useFor = true, bool useHost = true, bool useProto = true, bool usePathBase = true, bool append = true)
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
            if (usePathBase)
            {
                context.RequestTransforms.Add(new RequestHeaderXForwardedPathBaseTransform(headerPrefix + ForwardedTransformFactory.PathBaseKey, append));
            }
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will add the Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static ProxyRoute WithTransformForwarded(this ProxyRoute proxyRoute, bool useFor = true, bool useHost = true, bool useProto = true, bool useBy = true, bool append = true, string forFormat = "Random", string byFormat = "Random")
        {
            var headers = new List<string>();

            if (useFor)
            {
                headers.Add(ForwardedTransformFactory.ForKey);
            }

            if (useBy)
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

            return proxyRoute.WithTransform(transform =>
            {
                transform[ForwardedTransformFactory.ForwardedKey] = string.Join(',', headers);
                transform[ForwardedTransformFactory.AppendKey] = append.ToString();

                if (useFor)
                {
                    transform.Add(ForwardedTransformFactory.ForFormatKey, forFormat);
                }

                if (useBy)
                {
                    transform.Add(ForwardedTransformFactory.ByFormatKey, byFormat);
                }
            });
        }

        /// <summary>
        /// Adds the transform which will add the Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static TransformBuilderContext AddForwarded(this TransformBuilderContext context,
            bool useFor = true, bool useHost = true, bool useProto = true, bool useBy = true, bool append = true,
            RequestHeaderForwardedTransform.NodeFormat forFormat = RequestHeaderForwardedTransform.NodeFormat.Random,
            RequestHeaderForwardedTransform.NodeFormat byFormat = RequestHeaderForwardedTransform.NodeFormat.Random)
        {
            context.UseDefaultForwarders = false;
            if (useBy || useFor || useHost || useProto)
            {
                var random = context.Services.GetRequiredService<IRandomFactory>();
                context.RequestTransforms.Add(new RequestHeaderForwardedTransform(random, forFormat, byFormat, useHost, useProto, append));
            }
            return context;
        }

        /// <summary>
        /// Clones the route and adds the transform which will set the given header with the Base64 encoded client certificate.
        /// </summary>
        public static ProxyRoute WithTransformClientCertHeader(this ProxyRoute proxyRoute, string headerName)
        {
            return proxyRoute.WithTransform(transform =>
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
