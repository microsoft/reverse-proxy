// <copyright file="BackendDynamicState.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using IslandGateway.Utilities;

namespace IslandGateway.Core.RuntimeModel
{
    internal sealed class BackendDynamicState
    {
        public BackendDynamicState(
            IReadOnlyList<EndpointInfo> allEndpoints,
            IReadOnlyList<EndpointInfo> healthyEndpoints)
        {
            Contracts.CheckValue(allEndpoints, nameof(allEndpoints));
            Contracts.CheckValue(healthyEndpoints, nameof(healthyEndpoints));

            this.AllEndpoints = allEndpoints;
            this.HealthyEndpoints = healthyEndpoints;
        }

        public IReadOnlyList<EndpointInfo> AllEndpoints { get; }

        public IReadOnlyList<EndpointInfo> HealthyEndpoints { get; }
    }
}
