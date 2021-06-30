// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Abstractions;
using Yarp.ReverseProxy.Service.HealthChecks;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Middleware
{
    public class PassiveHealthCheckMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly IDictionary<string, IPassiveHealthCheckPolicy> _policies;

        public PassiveHealthCheckMiddleware(RequestDelegate next, IEnumerable<IPassiveHealthCheckPolicy> policies)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _policies = policies?.ToDictionaryByUniqueId(p => p.Name) ?? throw new ArgumentNullException(nameof(policies));
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);

            var proxyFeature = context.GetReverseProxyFeature();
            var options = proxyFeature.ClusterSnapshot.Options.HealthCheck?.Passive;

            // Do nothing if no target destination has been chosen for the request.
            if (!(options?.Enabled).GetValueOrDefault() || proxyFeature.ProxiedDestination == null)
            {
                return;
            }

            var policy = _policies.GetRequiredServiceById(options.Policy, HealthCheckConstants.PassivePolicy.TransportFailureRate);
            var cluster = context.GetRouteConfig().Cluster;
            policy.RequestProxied(cluster, proxyFeature.ProxiedDestination, context);
        }
    }
}
