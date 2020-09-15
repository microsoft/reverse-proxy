// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions
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

        internal static bool Equals(LoadBalancingOptions options1, LoadBalancingOptions options2)
        {
            if (options1 == null && options2 == null)
            {
                return true;
            }

            if (options1 == null || options2 == null)
            {
                return false;
            }

            return options1.Mode == options2.Mode;
        }
    }
}
