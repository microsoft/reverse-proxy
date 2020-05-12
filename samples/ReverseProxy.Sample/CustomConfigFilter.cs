// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.Service;

namespace Microsoft.ReverseProxy.Sample
{
    public class CustomConfigFilter : IProxyConfigFilter
    {
        public Task ConfigureBackendAsync(Backend backend, CancellationToken cancel)
        {
            backend.HealthCheckOptions ??= new HealthCheckOptions();
            // How to use custom metadata to configure backends
            if (backend.Metadata?.TryGetValue("CustomHealth", out var customHealth) ?? false
                && string.Equals(customHealth, "true", StringComparison.OrdinalIgnoreCase))
            {
                backend.HealthCheckOptions.Enabled = true;
            }

            // Or wrap the meatadata in config sugar
            var config = new ConfigurationBuilder().AddInMemoryCollection(backend.Metadata).Build();
            backend.HealthCheckOptions.Enabled = config.GetValue<bool>("CustomHealth");

            return Task.CompletedTask;
        }

        public Task ConfigureRouteAsync(ProxyRoute route, CancellationToken cancel)
        {
            // Do not let config based routes take priority over code based routes.
            // Lower numbers are higher priority.
            if (route.Priority.HasValue && route.Priority.Value < 0)
            {
                route.Priority = 0;
            }

            return Task.CompletedTask;
        }
    }
}
