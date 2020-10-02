// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    /// <summary>
    /// Watches for requests proxying results to evaluate destinations passive health states.
    /// </summary>
    public interface IPassiveHealthCheckWatcher
    {
        /// <param name="cluster">Request's cluster.</param>
        /// <param name="destination">Request's destination.</param>
        /// <param name="context">Context.</param>
        /// <param name="error">If a request failed, it contains the error occurred. Otherwise, null.</param>
        void RequestProxied(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error);
    }
}
