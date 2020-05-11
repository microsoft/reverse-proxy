// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    internal sealed class BackendDynamicState
    {
        public BackendDynamicState(
            IReadOnlyList<DestinationInfo> allDestinations,
            IReadOnlyList<DestinationInfo> healthyDestinations)
        {
            Contracts.CheckValue(allDestinations, nameof(allDestinations));
            Contracts.CheckValue(healthyDestinations, nameof(healthyDestinations));

            AllDestinations = allDestinations;
            HealthyDestinations = healthyDestinations;
        }

        public IReadOnlyList<DestinationInfo> AllDestinations { get; }

        public IReadOnlyList<DestinationInfo> HealthyDestinations { get; }
    }
}
