// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Service.RuntimeModel.Transforms
{
    public abstract class RequestParametersTransform
    {
        public abstract void Apply(RequestParametersTransformContext context);
    }
}
