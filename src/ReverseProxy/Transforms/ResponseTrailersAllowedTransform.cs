// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms
{
    /// <summary>
    /// Copies only allowed response trailers.
    /// </summary>
    public class ResponseTrailersAllowedTransform : ResponseTrailersTransform
    {
        public ResponseTrailersAllowedTransform(string[] allowedHeaders)
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
        public override ValueTask ApplyAsync(ResponseTrailersTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(!context.HeadersCopied);

            // See https://github.com/microsoft/reverse-proxy/blob/51d797986b1fea03500a1ad173d13a1176fb5552/src/ReverseProxy/Forwarder/HttpTransformer.cs#L85-L99
            // NOTE: Deliberately not using `context.Response.SupportsTrailers()`, `context.Response.AppendTrailer(...)`
            // because they lookup `IHttpResponseTrailersFeature` for every call. Here we do it just once instead.
            var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
            var outgoingTrailers = responseTrailersFeature?.Trailers;
            if (outgoingTrailers != null && !outgoingTrailers.IsReadOnly)
            {
                // Note that trailers, if any, should already have been declared in Proxy's response
                CopyResponseHeaders(context.ProxyResponse.TrailingHeaders, outgoingTrailers);
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
