// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Load balancing options.
    /// </summary>
    public sealed class LoadBalancingOptions
    {
        public LoadBalancingMode Mode { get; set; }

        internal LoadBalancingOptions DeepClone()
        {
            return new LoadBalancingOptions
            {
                Mode = Mode,
            };
        }
    }
}
