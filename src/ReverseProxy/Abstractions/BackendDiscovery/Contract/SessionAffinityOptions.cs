// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract
{
    /// <summary>
    /// Session affinitity options.
    /// </summary>
    public sealed class SessionAffinityOptions
    {
        public SessionAffinityMode Mode { get; set; }

        public string CustomHeaderName { get; set; }

        internal SessionAffinityOptions DeepClone()
        {
            return new SessionAffinityOptions
            {
                Mode = Mode,
                CustomHeaderName = CustomHeaderName
            };
        }
    }
}
