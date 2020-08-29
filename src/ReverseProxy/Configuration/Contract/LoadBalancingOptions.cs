// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Configuration.Contract
{
    /// <summary>
    /// Load balancing options.
    /// </summary>
    public sealed class LoadBalancingOptions
    {
        public LoadBalancingMode Mode { get; set; }
    }
}
