// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    public interface ITransformBuilder
    {
        void Build(IList<IDictionary<string, string>> transforms, out IReadOnlyList<RequestParametersTransform> requestParamterTransforms);
    }
}
