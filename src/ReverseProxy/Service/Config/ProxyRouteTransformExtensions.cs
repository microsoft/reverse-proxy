using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    public static class ProxyRouteTransformExtensions
    {
        public static void AddPathSetTransform(this ProxyRoute proxyRoute, PathString path)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathSet"] = path,
            });
        }

        public static void AddPathPrefixTransform(this ProxyRoute proxyRoute, PathString prefix)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathPrefix"] = prefix,
            });
        }

        public static void AddPathRemovePrefixTransform(this ProxyRoute proxyRoute, PathString prefix)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathRemovePrefix"] = prefix,
            });
        }

        public static void AddPathRouteValuesTransform(this ProxyRoute proxyRoute, string pattern)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["PathPattern"] = pattern,
            });
        }

        public static void CopyRequestHeaders(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeadersCopy"] = "True",
            });
        }

        public static void SuppressRequestHeaders(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeadersCopy"] = "False",
            });
        }

        public static void AddRequestHeaderOriginalHost(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeaderOriginalHost"] = "True",
            });
        }

        public static void SuppressRequestHeaderOriginalHost(this ProxyRoute proxyRoute)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeaderOriginalHost"] = "False",
            });
        }

        public static void AddRequestHeaderTransform(this ProxyRoute proxyRoute, string headerName, string value, bool append)
        {
            var type = append ? "Append" : "Set";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["RequestHeader"] = headerName,
                [type] = value
            });
        }

        public static void AddResponseHeaderTransform(this ProxyRoute proxyRoute, string headerName, string value, bool append, bool always)
        {
            var type = append ? "Append" : "Set";
            var when = always ? "always" : "-";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ResponseHeader"] = headerName,
                [type] = value,
                ["When"] = when
            });
        }

        public static void AddResponseTrailerTransform(this ProxyRoute proxyRoute, string headerName, string value, bool append, bool always)
        {
            var type = append ? "Append" : "Set";
            var when = always ? "always" : "-";
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ResponseTrailer"] = headerName,
                [type] = value,
                ["When"] = when
            });
        }

        public static void AddClientCertTransform(this ProxyRoute proxyRoute, string headerName)
        {
            proxyRoute.Transforms.Add(new Dictionary<string, string>
            {
                ["ClientCert"] = headerName
            });
        }

        public static void AddForwardedTransform(this ProxyRoute proxyRoute, bool useFor, bool useBy, bool useHost, bool useProto, bool append, string forFormat, string byFormat)
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
    }
}
