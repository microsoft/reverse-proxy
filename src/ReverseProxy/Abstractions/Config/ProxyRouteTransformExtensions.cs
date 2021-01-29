// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Abstractions.Config
{
    /// <summary>
    /// Extensions for adding transforms to ProxyRoute.
    /// </summary>
    public static class ProxyRouteTransformExtensions
    {
        /// <summary>
        /// Clones the ProxyRoute and adds the transform.
        /// </summary>
        /// <returns>The cloned route with the new transform.</returns>
        public static ProxyRoute WithTransform(this ProxyRoute proxyRoute, Action<IDictionary<string, string>> createTransform)
        {
            if (createTransform is null)
            {
                throw new ArgumentNullException(nameof(createTransform));
            }

            List<IReadOnlyDictionary<string, string>> transforms;
            if (proxyRoute.Transforms == null)
            {
                transforms = new List<IReadOnlyDictionary<string, string>>();
            }
            else
            {
                transforms = new List<IReadOnlyDictionary<string, string>>(proxyRoute.Transforms.Count + 1);
                transforms.AddRange(proxyRoute.Transforms);
            }

            var transform = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            createTransform(transform);
            transforms.Add(transform);

            return proxyRoute with { Transforms = transforms };
        }

        /// <summary>
        /// Clones the route and adds the transform which will add Base64 encoded client certificate to the given header name.
        /// </summary>
        public static ProxyRoute WithTransformClientCert(this ProxyRoute proxyRoute, string headerName)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform["ClientCert"] = headerName;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will add X-Forwarded-* headers.
        /// </summary>
        public static ProxyRoute WithTransformXForwarded(this ProxyRoute proxyRoute, string headerPrefix = "X-Forwarded", bool useFor = true, bool useHost = true, bool useProto = true, bool usePathBase = true, bool append = true)
        {
            var headers = new List<string>();

            if (useFor)
            {
                headers.Add("For");
            }

            if (usePathBase)
            {
                headers.Add("PathBase");
            }

            if (useHost)
            {
                headers.Add("Host");
            }

            if (useProto)
            {
                headers.Add("Proto");
            }

            return proxyRoute.WithTransform(transform =>
            {
                transform["X-Forwarded"] = string.Join(',', headers);
                transform["Append"] = append.ToString();
                transform["Prefix"] = headerPrefix;
            });
        }

        /// <summary>
        /// Clones the route and adds the transform which will add Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static ProxyRoute WithTransformForwarded(this ProxyRoute proxyRoute, bool useFor = true, bool useHost = true, bool useProto = true, bool useBy = true, bool append = true, string forFormat = "Random", string byFormat = "Random")
        {
            var headers = new List<string>();

            if (useFor)
            {
                headers.Add("For");
            }

            if (useBy)
            {
                headers.Add("By");
            }

            if (useHost)
            {
                headers.Add("Host");
            }

            if (useProto)
            {
                headers.Add("Proto");
            }

            return proxyRoute.WithTransform(transform =>
            {
                transform["Forwarded"] = string.Join(',', headers);
                transform["Append"] = append.ToString();

                if (useFor)
                {
                    transform.Add("ForFormat", forFormat);
                }

                if (useBy)
                {
                    transform.Add("ByFormat", byFormat);
                }
            });
        }

        /// <summary>
        /// Clones the route and adds the transform that will replace the HTTP method if it matches.
        /// </summary>
        public static ProxyRoute WithTransformHttpMethod(this ProxyRoute proxyRoute, string fromHttpMethod, string toHttpMethod)
        {
            return proxyRoute.WithTransform(transform =>
            {
                transform["HttpMethod"] = fromHttpMethod;
                transform["Set"] = toHttpMethod;
            });
        }
    }
}
