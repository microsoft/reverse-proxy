// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Sample.Config
{
    internal class ProxyConfigRoot
    {
        /// <summary>
        /// Discovery mechanism used to configure Backends, Endpoints and Routes in Reverse Proxy.
        /// Accepted values are:
        /// <list type="bullet">
        ///   <item>
        ///     <c>static</c>, in which case <see cref="StaticDiscoveryOptions"/>
        ///     should specify the static configuration values.
        ///   </item>
        /// </list>
        /// </summary>
        public string DiscoveryMechanism { get; set; }

        /// <summary>
        /// Options that apply when <see cref="DiscoveryMechanism"/> is <c>static</c>.
        /// </summary>
        public StaticDiscoveryOptions StaticDiscoveryOptions { get; set; }
    }
}
