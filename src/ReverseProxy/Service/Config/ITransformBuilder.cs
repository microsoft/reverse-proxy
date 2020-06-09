// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Service.RuntimeModel.Transforms;

namespace Microsoft.ReverseProxy.Service.Config
{
    public interface ITransformBuilder
    {
        bool Validate(IList<IDictionary<string, string>> transforms, string routeId, IConfigErrorReporter errorReporter);

        void Build(IList<IDictionary<string, string>> rawTransforms, out Transforms transforms);
    }
}
