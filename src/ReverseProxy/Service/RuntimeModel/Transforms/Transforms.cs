// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class Transforms
    {
        // For tests
        internal static Transforms Empty { get; } = new Transforms(
            copyRequestHeaders: null,
            requestTransforms: Array.Empty<RequestParametersTransform>(),
            requestHeaderTransforms: new Dictionary<string, RequestHeaderTransform>(),
            responseHeaderTransforms: new Dictionary<string, ResponseHeaderTransform>(),
            responseTrailerTransforms: new Dictionary<string, ResponseHeaderTransform>());

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

        public bool? CopyRequestHeaders { get; }

        public IReadOnlyList<RequestParametersTransform> RequestTransforms { get; }

        public IReadOnlyDictionary<string, RequestHeaderTransform> RequestHeaderTransforms { get; }

        public IReadOnlyDictionary<string, ResponseHeaderTransform> ResponseHeaderTransforms { get; }

        public IReadOnlyDictionary<string, ResponseHeaderTransform> ResponseTrailerTransforms { get; }
    }
}
