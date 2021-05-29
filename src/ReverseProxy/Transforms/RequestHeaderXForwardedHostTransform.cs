// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Primitives;

namespace Yarp.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Sets or appends the X-Forwarded-Host header with the request's original Host header.
    /// </summary>
    public class RequestHeaderXForwardedHostTransform : RequestTransform
    {
        /// <summary>
        /// Creates a new transform.
        /// </summary>
        /// <param name="append">Indicates if the new value should append to or replace an existing header.</param>
        public RequestHeaderXForwardedHostTransform(string headerName, bool append)
        {
            if (string.IsNullOrEmpty(headerName))
            {
                throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
            }

            HeaderName = headerName;
            Append = append;
        }

        internal string HeaderName { get; }
        internal bool Append { get; }

        /// <inheritdoc/>
        public override ValueTask ApplyAsync(RequestTransformContext context)
        {
            if (context is null)
            {
                throw new System.ArgumentNullException(nameof(context));
            }

            var existingValues = TakeHeader(context, HeaderName);

            var host = context.HttpContext.Request.Host;

            if (!host.HasValue)
            {
                if (Append && !string.IsNullOrEmpty(existingValues))
                {
                    AddHeader(context, HeaderName, existingValues);
                }
            }
            else if (Append)
            {
                var values = StringValues.Concat(existingValues, host.ToUriComponent());
                AddHeader(context, HeaderName, values);
            }
            else
            {
                // Set
                AddHeader(context, HeaderName, host.ToUriComponent());
            }

            return default;
        }
    }
}
