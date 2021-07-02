// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Transforms
{
    /// <summary>
    /// Removes a request header.
    /// </summary>
    public class RequestHeadersAllowedTransform : RequestTransform
    {
        public RequestHeadersAllowedTransform(string[] allowedHeaders)
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
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            Debug.Assert(!context.HeadersCopied);

            foreach (var header in context.HttpContext.Request.Headers)
            {
                var headerName = header.Key;
                var headerValue = header.Value;
                if (!StringValues.IsNullOrEmpty(headerValue)
                    && AllowedHeadersSet.Contains(headerName))
                {
                    AddHeader(context, headerName, headerValue);
                }
            }

            context.HeadersCopied = true;

            return default;
        }
    }
}
