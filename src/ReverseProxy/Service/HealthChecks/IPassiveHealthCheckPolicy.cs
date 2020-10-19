// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Passive health check evaluation policy.
    /// </summary>
    public interface IPassiveHealthCheckPolicy
    {
        /// <summary>
        /// Policy's name.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Registers a successful or failed request and evaluates a new <see cref="CompositeDestinationHealth.Passive"/> value.
        /// </summary>
        /// <param name="cluster">Request's cluster.</param>
        /// <param name="destination">Request's destination.</param>
        /// <param name="context">Context.</param>
        /// <param name="error">Error occurred while proxying a request.</param>
        void RequestProxied(ClusterInfo cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error);
    }
}
