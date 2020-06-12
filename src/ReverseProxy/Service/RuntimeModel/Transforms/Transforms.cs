// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    /// <summary>
    /// Transforms for a given route.
    /// </summary>
    public class Transforms
    {
        // For tests
        internal static Transforms Empty { get; } = new Transforms(
            copyRequestHeaders: null,
            requestTransforms: Array.Empty<RequestParametersTransform>(),
            requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>(),
            responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(),
            responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>());

        /// <summary>
        /// Creates a new <see cref="Transforms"/> instance.
        /// </summary>
        public Transforms(bool? copyRequestHeaders, IReadOnlyList<RequestParametersTransform> requestTransforms,
            IReadOnlyDictionary<string, RequestHeaderTransform> requestHeaderTransforms,
            IReadOnlyDictionary<string, ResponseHeaderTransform> responseHeaderTransforms,
            IReadOnlyDictionary<string, ResponseHeaderTransform> responseTrailerTransforms)
        {
            CopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms ?? throw new ArgumentNullException(nameof(requestTransforms));
            RequestHeaderTransforms = requestHeaderTransforms ?? throw new ArgumentNullException(nameof(requestHeaderTransforms));
            ResponseHeaderTransforms = responseHeaderTransforms ?? throw new ArgumentNullException(nameof(responseHeaderTransforms));
            ResponseTrailerTransforms = responseTrailerTransforms ?? throw new ArgumentNullException(nameof(responseTrailerTransforms));
        }

        /// <summary>
        /// Indicates if all request headers should be proxied in absence of other transforms.
        /// </summary>
        public bool? CopyRequestHeaders { get; }

        /// <summary>
        /// Request parameter transforms.
        /// </summary>
        public IReadOnlyList<RequestParametersTransform> RequestTransforms { get; }

        /// <summary>
        /// Request header transforms.
        /// </summary>
        public IReadOnlyDictionary<string, RequestHeaderTransform> RequestHeaderTransforms { get; }

        /// <summary>
        /// Response header transforms.
        /// </summary>
        public IReadOnlyDictionary<string, ResponseHeaderTransform> ResponseHeaderTransforms { get; }

        /// <summary>
        /// Response trailer transforms.
        /// </summary>
        public IReadOnlyDictionary<string, ResponseHeaderTransform> ResponseTrailerTransforms { get; }
    }
}
