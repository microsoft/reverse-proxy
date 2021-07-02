// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms
{
    /// <summary>
    /// Removes a request header.
    /// </summary>
    public class ResponseHeadersAllowedTransform : ResponseTransform
    {
        public ResponseHeadersAllowedTransform(string[] allowedHeaders)
        {
            if (allowedHeaders is null)
            {
                throw new ArgumentNullException(nameof(allowedHeaders));
            }

            AllowedHeaders = allowedHeaders;
            AllowedHeadersSet = new HashSet<string>(allowedHeaders, StringComparer.OrdinalIgnoreCase);
        }

        internal string[] AllowedHeaders { get; }

        private HashSet<string> AllowedHeadersSet { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(ResponseTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(!context.HeadersCopied);

            // See https://github.com/microsoft/reverse-proxy/blob/51d797986b1fea03500a1ad173d13a1176fb5552/src/ReverseProxy/Forwarder/HttpTransformer.cs#L67-L77
            var responseHeaders = context.HttpContext.Response.Headers;
            CopyResponseHeaders(context.ProxyResponse.Headers, responseHeaders);
            if (context.ProxyResponse.Content != null)
            {
                CopyResponseHeaders(context.ProxyResponse.Content.Headers, responseHeaders);
            }

            context.HeadersCopied = true;

            return default;
        }

        // See https://github.com/microsoft/reverse-proxy/blob/51d797986b1fea03500a1ad173d13a1176fb5552/src/ReverseProxy/Forwarder/HttpTransformer.cs#L102-L115
        private void CopyResponseHeaders(HttpHeaders source, IHeaderDictionary destination)
        {
            foreach (var header in source)
            {
                var headerName = header.Key;
                if (AllowedHeadersSet.Contains(headerName))
                {
                    Debug.Assert(header.Value is string[]);
                    destination.Append(headerName, header.Value as string[] ?? header.Value.ToArray());
                }
            }
        }
    }
}
