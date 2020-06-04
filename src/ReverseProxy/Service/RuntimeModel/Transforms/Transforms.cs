// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class Transforms
    {
        public Transforms(IReadOnlyList<RequestParametersTransform> requestTransforms, bool? copyRequestHeaders,
            IReadOnlyDictionary<string, RequestHeaderTransform> requestHeaderTransforms,
            IReadOnlyDictionary<string, ResponseHeaderTransform> responseHeaderTransforms,
            IReadOnlyDictionary<string, ResponseHeaderTransform> responseTrailerTransforms)
        {
            CopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms;
            RequestHeaderTransforms = requestHeaderTransforms;
            ResponseHeaderTransforms = responseHeaderTransforms;
            ResponseTrailerTransforms = responseTrailerTransforms;
        }

        public bool? CopyRequestHeaders { get; }

        public IReadOnlyList<RequestParametersTransform> RequestTransforms { get; }

        public IReadOnlyDictionary<string, RequestHeaderTransform> RequestHeaderTransforms { get; }

        public IReadOnlyDictionary<string, ResponseHeaderTransform> ResponseHeaderTransforms { get; }

        public IReadOnlyDictionary<string, ResponseHeaderTransform> ResponseTrailerTransforms { get; }
    }
}
