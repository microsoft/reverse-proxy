using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    public static class ProxyRouteTransformExtensions
    {
        public static void AddTransformPathSet(this ProxyRoute proxyRoute, PathString path)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathSet"] = path.Value,
            });
        }

        public static void AddTransformPathPrefix(this ProxyRoute proxyRoute, PathString prefix)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathPrefix"] = prefix.Value,
            });
        }

        public static void AddTransformPathRemovePrefix(this ProxyRoute proxyRoute, PathString prefix)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathRemovePrefix"] = prefix.Value,
            });
        }

        public static void AddTransformPathRouteValues(this ProxyRoute proxyRoute, PathString pattern)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathPattern"] = pattern.Value,
            });
        }

        public static void AddTransformSuppressRequestHeaders(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeadersCopy"] = "False",
            });
        }

        public static void AddTransformUseOriginalHostHeader(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeaderOriginalHost"] = "True",
            });
        }

        public static void AddTransformRequestHeader(this ProxyRoute proxyRoute, string headerName, string value, bool append = true)
        {
            var type = append ? "Append" : "Set";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeader"] = headerName,
                [type] = value
            });
        }

        public static void AddTransformResponseHeader(this ProxyRoute proxyRoute, string headerName, string value, bool append = true, bool always = true)
        {
            var type = append ? "Append" : "Set";
            var when = always ? "always" : "success";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ResponseHeader"] = headerName,
                [type] = value,
                ["When"] = when
            });
        }

        public static void AddTransformResponseTrailer(this ProxyRoute proxyRoute, string headerName, string value, bool append = true, bool always = true)
        {
            var type = append ? "Append" : "Set";
            var when = always ? "always" : "success";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ResponseTrailer"] = headerName,
                [type] = value,
                ["When"] = when
            });
        }

        public static void AddTransformClientCert(this ProxyRoute proxyRoute, string headerName)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ClientCert"] = headerName
            });
        }

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

            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["X-Forwarded"] = string.Join(',', headers),
                ["Append"] = append.ToString(),
                ["Prefix"] = headerPrefix
            });
        }

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

            proxyRoute.Transforms.Add(transform);
        }

        public static void AddTransformHttpMethod(this ProxyRoute proxyRoute, string fromHttpMethod, string toHttpMethod)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["HttpMethod"] = fromHttpMethod,
                ["Set"] = toHttpMethod
            });
        }
    }
}
