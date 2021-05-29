// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using Yarp.ReverseProxy.Service.Proxy;

namespace Yarp.ReverseProxy.Utilities
{
    /// <summary>
    /// APIs that can be used when transforming requests.
    /// </summary>
    public static class RequestUtilities
    {
        /// <summary>
        /// Converts the given HTTP method (usually obtained from <see cref="HttpRequest.Method"/>)
        /// into the corresponding <see cref="HttpMethod"/> static instance.
        /// </summary>
        public static HttpMethod GetHttpMethod(string method) => method switch
        {
            string mth when HttpMethods.IsGet(mth) => HttpMethod.Get,
            string mth when HttpMethods.IsPost(mth) => HttpMethod.Post,
            string mth when HttpMethods.IsPut(mth) => HttpMethod.Put,
            string mth when HttpMethods.IsDelete(mth) => HttpMethod.Delete,
            string mth when HttpMethods.IsOptions(mth) => HttpMethod.Options,
            string mth when HttpMethods.IsHead(mth) => HttpMethod.Head,
            string mth when HttpMethods.IsPatch(mth) => HttpMethod.Patch,
            string mth when HttpMethods.IsTrace(mth) => HttpMethod.Trace,
            // NOTE: Proxying "CONNECT" is not supported (by design!)
            string mth when HttpMethods.IsConnect(mth) => throw new NotSupportedException($"Unsupported request method '{method}'."),
            _ => new HttpMethod(method)
        };

        internal static bool ShouldSkipRequestHeader(string headerName)
        {
            if (_headersToExclude.Contains(headerName))
            {
                return true;
            }

            // Filter out HTTP/2 pseudo headers like ":method" and ":path", those go into other fields.
            if (headerName.StartsWith(':'))
            {
                return true;
            }

            return false;
        }

        internal static bool ShouldSkipResponseHeader(string headerName)
        {
            return _headersToExclude.Contains(headerName);
        }

        private static readonly HashSet<string> _headersToExclude = new(14, StringComparer.OrdinalIgnoreCase)
        {
            HeaderNames.Connection,
            HeaderNames.TransferEncoding,
            HeaderNames.KeepAlive,
            HeaderNames.Upgrade,
            "Proxy-Connection",
            "Proxy-Authenticate",
            "Proxy-Authentication-Info",
            "Proxy-Authorization",
            "Proxy-Features",
            "Proxy-Instruction",
            "Security-Scheme",
            "ALPN",
            "Close",
#if NET
            HeaderNames.AltSvc,
#else
            "Alt-Svc",
#endif

        };

        // Headers marked as HttpHeaderType.Content in
        // https://github.com/dotnet/runtime/blob/main/src/libraries/System.Net.Http/src/System/Net/Http/Headers/KnownHeaders.cs
        private static readonly HashSet<string> _contentHeaders = new(11, StringComparer.OrdinalIgnoreCase)
        {
            HeaderNames.Allow,
            HeaderNames.ContentDisposition,
            HeaderNames.ContentEncoding,
            HeaderNames.ContentLanguage,
            HeaderNames.ContentLength,
            HeaderNames.ContentLocation,
            HeaderNames.ContentMD5,
            HeaderNames.ContentRange,
            HeaderNames.ContentType,
            HeaderNames.Expires,
            HeaderNames.LastModified
        };

        /// <summary>
        /// Appends the given path and query to the destination prefix while avoiding duplicate '/'.
        /// </summary>
        /// <param name="destinationPrefix">The scheme, host, port, and optional path base for the destination server.
        /// e.g. "http://example.com:80/path/prefix"</param>
        /// <param name="path">The path to append.</param>
        /// <param name="query">The query to append</param>
        public static Uri MakeDestinationAddress(string destinationPrefix, PathString path, QueryString query)
        {
            ReadOnlySpan<char> prefixSpan = destinationPrefix;

            if (path.HasValue && destinationPrefix.EndsWith('/'))
            {
                // When PathString has a value it always starts with a '/'. Avoid double slashes when concatenating.
                prefixSpan = prefixSpan[0..^1];
            }

            var targetAddress = string.Concat(prefixSpan, path.ToUriComponent(), query.ToUriComponent());

            return new Uri(targetAddress, UriKind.Absolute);
        }

        // Note: HttpClient.SendAsync will end up sending the union of
        // HttpRequestMessage.Headers and HttpRequestMessage.Content.Headers.
        // We don't really care where the proxied headers appear among those 2,
        // as long as they appear in one (and only one, otherwise they would be duplicated).
        // Some headers may only appear on HttpContentHeaders, in which case we inject
        // an EmptyHttpContent - dummy 0-length container only used for headers.
        internal static void AddHeader(HttpRequestMessage request, string headerName, StringValues value)
        {
            // HttpClient wrongly uses comma (",") instead of semi-colon (";") as a separator for Cookie headers.
            // To mitigate this, we concatenate them manually and put them back as a single header value.
            // A multi-header cookie header is invalid, but we get one because of
            // https://github.com/dotnet/aspnetcore/issues/26461
            if (string.Equals(headerName, HeaderNames.Cookie, StringComparison.OrdinalIgnoreCase) && value.Count > 1)
            {
                value = string.Join("; ", value);
            }

            if (value.Count == 1)
            {
                string headerValue = value;
                if (!request.Headers.TryAddWithoutValidation(headerName, headerValue))
                {
                    if (request.Content is null && _contentHeaders.Contains(headerName))
                    {
                        request.Content = new EmptyHttpContent();
                    }

                    var added = request.Content?.Headers.TryAddWithoutValidation(headerName, headerValue);
                    // TODO: Log
                    Debug.Assert(added.GetValueOrDefault(), $"A header was dropped; {headerName}: {headerValue}");
                }
            }
            else
            {
                string[] headerValues = value;
                if (!request.Headers.TryAddWithoutValidation(headerName, headerValues))
                {
                    if (request.Content is null && _contentHeaders.Contains(headerName))
                    {
                        request.Content = new EmptyHttpContent();
                    }

                    var added = request.Content?.Headers.TryAddWithoutValidation(headerName, headerValues);
                    // TODO: Log
                    Debug.Assert(added.GetValueOrDefault(), $"A header was dropped; {headerName}: {string.Join(", ", headerValues)}");
                }
            }

#if DEBUG
            if (request.Content is EmptyHttpContent content && content.Headers.TryGetValues(HeaderNames.ContentLength, out var contentLength))
            {
                Debug.Assert(contentLength.Single() == "0", "An actual content should have been set");
            }
#endif
        }
    }
}
