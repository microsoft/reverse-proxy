// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract
{
    /// <summary>
    /// Defines customer header specific affinity provider options.
    /// </summary>
    public class CustomHeaderSessionAffinityProviderOptions
    {
        public static readonly string DefaultCustomHeaderName = "X-Microsoft-Proxy-Affinity";

        /// <summary>
        /// Name of a custom header storing an affinity key.
        /// </summary>
        public string CustomHeaderName { get; set; } = DefaultCustomHeaderName;
    }
}
