// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    internal sealed class BackendDynamicState
    {
        public BackendDynamicState(
            IReadOnlyList<EndpointInfo> allEndpoints,
            IReadOnlyList<EndpointInfo> healthyEndpoints)
        {
            Contracts.CheckValue(allEndpoints, nameof(allEndpoints));
            Contracts.CheckValue(healthyEndpoints, nameof(healthyEndpoints));

            AllEndpoints = allEndpoints;
            HealthyEndpoints = healthyEndpoints;
        }

        public IReadOnlyList<EndpointInfo> AllEndpoints { get; }

        public IReadOnlyList<EndpointInfo> HealthyEndpoints { get; }
    }
}
