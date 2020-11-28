// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public readonly struct ClusterLoadBalancingOptions
    {
        public ClusterLoadBalancingOptions(LoadBalancingMode mode)
        {
            Mode = mode;
            // Increment returns the new value and we want the first return value to be 0.
            RoundRobinState = new AtomicCounter() { Value = -1 };
        }

        public LoadBalancingMode Mode { get; }

        internal AtomicCounter RoundRobinState { get; }
    }
}
