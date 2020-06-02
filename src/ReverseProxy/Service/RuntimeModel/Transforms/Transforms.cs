// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class Transforms
    {
        public Transforms(IReadOnlyList<RequestParametersTransform> requestTransforms, bool? copyRequestHeaders, IReadOnlyDictionary<string, RequestHeaderTransform> requestHeaderTransforms)
        {
            CopyRequestHeaders = copyRequestHeaders;
            RequestTransforms = requestTransforms;
            RequestHeaderTransforms = requestHeaderTransforms;
        }

        public bool? CopyRequestHeaders { get; }

        public IReadOnlyList<RequestParametersTransform> RequestTransforms { get; }

        public IReadOnlyDictionary<string, RequestHeaderTransform> RequestHeaderTransforms { get; }
    }
}
