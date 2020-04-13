// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.RuntimeModel;

namespace Microsoft.ReverseProxy.Core.Middleware
{
    public class AvailableBackendEndpointsFeature
    {
        public IReadOnlyList<EndpointInfo> Endpoints { get; set; }
    }
}
