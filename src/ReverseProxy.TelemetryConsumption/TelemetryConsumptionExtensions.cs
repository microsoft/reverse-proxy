// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.ReverseProxy.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class TelemetryConsumptionExtensions
    {
#if NET5_0
        /// <summary>
        /// Registers all telemetry listeners (Proxy, Kestrel, Http, NameResolution, NetSecurity and Sockets).
        /// </summary>
#else
        /// <summary>
        /// Registers all telemetry listeners (Proxy and Kestrel).
        /// </summary>
#endif
        public static IServiceCollection AddTelemetryListeners(this IServiceCollection services)
        {
            services.AddHttpContextAccessor();
            services.TryAddSingleton(new ServiceCollectionInternal(services));

            services.AddHostedService<ProxyEventListenerService>();
            services.AddHostedService<KestrelEventListenerService>();
#if NET5_0
            services.AddHostedService<HttpEventListenerService>();
            services.AddHostedService<NameResolutionEventListenerService>();
            services.AddHostedService<NetSecurityEventListenerService>();
            services.AddHostedService<SocketsEventListenerService>();
#endif
            return services;
        }
    }
}
