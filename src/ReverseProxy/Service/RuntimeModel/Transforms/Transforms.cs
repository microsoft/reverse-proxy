// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public class Transforms
    {
        public Transforms(IReadOnlyList<RequestParametersTransform> requestTransforms)
        {
            RequestTransforms = requestTransforms;
        }

        public IReadOnlyList<RequestParametersTransform> RequestTransforms { get; }
    }
}
