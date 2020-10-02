// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Service.Proxy;
using Microsoft.ReverseProxy.Utilities;
using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class PassiveHealthCheckWatcher : IPassiveHealthCheckWatcher
    {
        private readonly IDictionary<string, IPassiveHealthCheckPolicy> _policies;

        public PassiveHealthCheckWatcher(IEnumerable<IPassiveHealthCheckPolicy> policies)
        {
            _policies = policies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(policies));
        }

        public void RequestProxied(ClusterConfig cluster, DestinationInfo destination, HttpContext context, IProxyErrorFeature error)
        {
            if (!cluster.HealthCheckOptions.Passive.Enabled)
            {
                return;
            }

            var policy = _policies.GetRequiredServiceById(cluster.HealthCheckOptions.Passive.Policy, HealthCheckConstants.PassivePolicy.TransportFailureRate);
            policy.RequestProxied(cluster, destination, context, error);
        }
    }
}