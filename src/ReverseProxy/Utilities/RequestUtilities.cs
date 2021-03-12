// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Yarp.ReverseProxy.Utilities
{
    internal static class RequestUtilities
    {
        internal static bool ShouldSkipResponseHeader(string headerName, bool isHttp2OrGreater)
        {
            if (isHttp2OrGreater)
            {
                return _invalidH2H3ResponseHeaders.Contains(headerName);
            }
            else
            {
                return headerName.Equals(HeaderNames.TransferEncoding, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static readonly HashSet<string> _invalidH2H3ResponseHeaders = new(StringComparer.OrdinalIgnoreCase)
        {
            HeaderNames.Connection,
            HeaderNames.TransferEncoding,
            HeaderNames.KeepAlive,
            HeaderNames.Upgrade,
            "Proxy-Connection"
        };

        /// <summary>
        /// Appends the given path and query to the destination prefix while avoiding duplicate '/'.
        /// </summary>
        /// <param name="destinationPrefix">The scheme, host, port, and possibly path base for the destination server.</param>
        /// <param name="path">The path to append.</param>
        /// <param name="query">The query to append</param>
        internal static Uri MakeDestinationAddress(string destinationPrefix, PathString path, QueryString query)
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
                    var added = request.Content?.Headers.TryAddWithoutValidation(headerName, headerValue);
                    // TODO: Log. Today this assert fails for a POST request with Content-Length: 0 header which is valid.
                    // https://github.com/microsoft/reverse-proxy/issues/618
                    // Debug.Assert(added.GetValueOrDefault(), $"A header was dropped; {headerName}: {headerValue}");
                }
            }
            else
            {
                string[] headerValues = value;
                if (!request.Headers.TryAddWithoutValidation(headerName, headerValues))
                {
                    var added = request.Content?.Headers.TryAddWithoutValidation(headerName, headerValues);
                    // TODO: Log. Today this assert fails for a POST request with Content-Length: 0 header which is valid.
                    // https://github.com/microsoft/reverse-proxy/issues/618
                    // Debug.Assert(added.GetValueOrDefault(), $"A header was dropped; {headerName}: {string.Join(", ", headerValues)}");
                }
            }
        }
    }
}
