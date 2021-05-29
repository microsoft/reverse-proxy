// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.Features;

namespace Yarp.ReverseProxy.Service.Model.Transforms
{
    /// <summary>
    /// Removes a response trailer.
    /// </summary>
    public class ResponseTrailerRemoveTransform : ResponseTrailersTransform
    {
        public ResponseTrailerRemoveTransform(string headerName, bool always)
        {
            if (string.IsNullOrEmpty(headerName))
            {
                throw new ArgumentException($"'{nameof(headerName)}' cannot be null or empty.", nameof(headerName));
            }

            HeaderName = headerName;
            Always = always;
        }

        internal string HeaderName { get; }

        internal bool Always { get; }

        // Assumes the response status code has been set on the HttpContext already.
        /// <inheritdoc/>
        public override ValueTask ApplyAsync(ResponseTrailersTransformContext context)
        {
            if (context is null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (Always || Success(context))
            {
                var responseTrailersFeature = context.HttpContext.Features.Get<IHttpResponseTrailersFeature>();
                var responseTrailers = responseTrailersFeature.Trailers;
                // Support should have already been checked by the caller.
                Debug.Assert(responseTrailers != null);
                Debug.Assert(!responseTrailers.IsReadOnly);

                responseTrailers.Remove(HeaderName);
            }

            return default;
        }
    }
}
