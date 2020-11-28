using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    public static class ProxyRouteTransformExtensions
    {
        /// <summary>
        /// Adds a transform to the route which set the request path with the given value.
        /// </summary>
        public static void AddTransformPathSet(this ProxyRoute proxyRoute, PathString path)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathSet"] = path.Value,
            });
        }

        /// <summary>
        /// Adds a transform to the route which will prefix the request path with the given value.
        /// </summary>
        public static void AddTransformPathPrefix(this ProxyRoute proxyRoute, PathString prefix)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathPrefix"] = prefix.Value,
            });
        }

        /// <summary>
        /// Adds a transform to the route which will remove the matching prefix from the request path.
        /// </summary>
        public static void AddTransformPathRemovePrefix(this ProxyRoute proxyRoute, PathString prefix)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathRemovePrefix"] = prefix.Value,
            });
        }

        /// <summary>
        /// Adds a transform to the route which will set the request path with the given value.
        /// </summary>
        public static void AddTransformPathRouteValues(this ProxyRoute proxyRoute, PathString pattern)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathPattern"] = pattern.Value,
            });
        }

        /// <summary>
        /// Adds a transform to the route which will prevent adding reqesut headers to the proxy request.
        /// </summary>
        public static void AddTransformSuppressRequestHeaders(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeadersCopy"] = "False",
            });
        }

        /// <summary>
        /// Adds a transform to the route which will copy the incoming request Host header to the proxy request.
        /// </summary>
        public static void AddTransformUseOriginalHostHeader(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeaderOriginalHost"] = "True",
            });
        }

        /// <summary>
        /// Adds a transform to the route which will append or set request header.
        /// </summary>
        public static void AddTransformRequestHeader(this ProxyRoute proxyRoute, string headerName, string value, bool append = true)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            var type = append ? "Append" : "Set";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeader"] = headerName,
                [type] = value
            });
        }

        /// <summary>
        /// Adds a transform to the route which will append or set response header.
        /// </summary>
        public static void AddTransformResponseHeader(this ProxyRoute proxyRoute, string headerName, string value, bool append = true, bool always = true)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            var type = append ? "Append" : "Set";
            var when = always ? "always" : "success";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ResponseHeader"] = headerName,
                [type] = value,
                ["When"] = when
            });
        }

        /// <summary>
        /// Adds a transform to the route which will append or set response trailer header.
        /// </summary>
        public static void AddTransformResponseTrailer(this ProxyRoute proxyRoute, string headerName, string value, bool append = true, bool always = true)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            var type = append ? "Append" : "Set";
            var when = always ? "always" : "success";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ResponseTrailer"] = headerName,
                [type] = value,
                ["When"] = when
            });
        }

        /// <summary>
        /// Adds a transform to the route which will add Base64 encoded client certificate to the given header name.
        /// </summary>
        public static void AddTransformClientCert(this ProxyRoute proxyRoute, string headerName)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ClientCert"] = headerName
            });
        }

        /// <summary>
        /// Adds a transform to the route which will add X-Forwarded-* headers.
        /// </summary>
        public static void AddTransformXForwarded(this ProxyRoute proxyRoute, string headerPrefix = "X-Forwarded", bool useFor = true, bool useHost = true, bool useProto = true, bool usePathBase = true, bool append = true)
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

            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["X-Forwarded"] = string.Join(',', headers),
                ["Append"] = append.ToString(),
                ["Prefix"] = headerPrefix
            });
        }

        /// <summary>
        /// Adds a transform to the route which will add Forwarded header as defined by [RFC 7239](https://tools.ietf.org/html/rfc7239).
        /// </summary>
        public static void AddTransformForwarded(this ProxyRoute proxyRoute, bool useFor = true, bool useHost = true, bool useProto = true, bool useBy = true, bool append = true, string forFormat = "Random", string byFormat = "Random")
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

            var transform = new Dictionary<string, string>
            {
                ["Forwarded"] = string.Join(',', headers),
                ["Append"] = append.ToString()
            };

            if (useFor)
            {
                transform.Add("ForFormat", forFormat);
            }

            if (useBy)
            {
                transform.Add("ByFormat", byFormat);
            }

            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();
            proxyRoute.Transforms.Add(transform);
        }

        /// <summary>
        /// Adds a transform to the route that will replace the HTTP method if it matches.
        /// </summary>
        public static void AddTransformHttpMethod(this ProxyRoute proxyRoute, string fromHttpMethod, string toHttpMethod)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["HttpMethod"] = fromHttpMethod,
                ["Set"] = toHttpMethod
            });
        }

        /// <summary>
        /// Adds a transform to the route that will append or set query from route value.
        /// </summary>
        public static void AddTransformQueryRouteParameter(this ProxyRoute proxyRoute, string queryKey, string routeValueKey, bool append = true)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            var type = append ? "Append" : "Set";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["QueryRouteParameter"] = queryKey,
                [type] = routeValueKey
            });
        }

        /// <summary>
        /// Adds a transform to the route that will append or set query from the given value.
        /// </summary>
        public static void AddTransformQueryValueParameter(this ProxyRoute proxyRoute, string queryKey, string value, bool append = true)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            var type = append ? "Append" : "Set";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["QueryValueParameter"] = queryKey,
                [type] = value
            });
        }

        /// <summary>
        /// Adds a transform to the route that will remove the given query key.
        /// </summary>
        public static void AddTransformRemoveQueryParameter(this ProxyRoute proxyRoute, string queryKey)
        {
            proxyRoute.Transforms ??= new List<IDictionary<string, string>>();

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["QueryRemoveParameter"] = queryKey
            });
        }
    }
}
