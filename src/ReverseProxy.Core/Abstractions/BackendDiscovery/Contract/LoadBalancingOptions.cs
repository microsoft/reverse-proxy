// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Load balancing options.
    /// </summary>
    public sealed class LoadBalancingOptions
    {
        internal LoadBalancingOptions DeepClone()
        {
            return new LoadBalancingOptions
            {
            };
        }
    }
}
