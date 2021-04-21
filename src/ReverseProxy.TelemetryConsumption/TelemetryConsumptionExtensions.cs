// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
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

        /// <summary>
        /// Registers a consumer singleton for every I*TelemetryConsumer interface it implements.
        /// </summary>
        public static IServiceCollection AddTelemetryConsumer(this IServiceCollection services, object consumer)
        {
            var implementsAny = false;

            if (consumer is IProxyTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IProxyTelemetryConsumer), consumer));
                implementsAny = true;
            }

            if (consumer is IKestrelTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IKestrelTelemetryConsumer), consumer));
                implementsAny = true;
            }

#if NET5_0
            if (consumer is IHttpTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IHttpTelemetryConsumer), consumer));
                implementsAny = true;
            }

            if (consumer is INameResolutionTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(INameResolutionTelemetryConsumer), consumer));
                implementsAny = true;
            }

            if (consumer is INetSecurityTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(INetSecurityTelemetryConsumer), consumer));
                implementsAny = true;
            }

            if (consumer is ISocketsTelemetryConsumer)
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(ISocketsTelemetryConsumer), consumer));
                implementsAny = true;
            }
#endif

            if (!implementsAny)
            {
                throw new ArgumentException("The consumer must implement at least one I*TelemetryConsumer interface.", nameof(consumer));
            }

            services.AddTelemetryListeners();

            return services;
        }

        /// <summary>
        /// Registers a <typeparamref name="TConsumer"/> singleton for every I*TelemetryConsumer interface it implements.
        /// </summary>
        public static IServiceCollection AddTelemetryConsumer<TConsumer>(this IServiceCollection services)
            where TConsumer : class
        {
            var implementsAny = false;

            if (typeof(IProxyTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IProxyTelemetryConsumer), provider => provider.GetRequiredService<TConsumer>(), ServiceLifetime.Singleton));
                implementsAny = true;
            }

            if (typeof(IKestrelTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IKestrelTelemetryConsumer), provider => provider.GetRequiredService<TConsumer>(), ServiceLifetime.Singleton));
                implementsAny = true;
            }

#if NET5_0
            if (typeof(IHttpTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(IHttpTelemetryConsumer), provider => provider.GetRequiredService<TConsumer>(), ServiceLifetime.Singleton));
                implementsAny = true;
            }

            if (typeof(INameResolutionTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(INameResolutionTelemetryConsumer), provider => provider.GetRequiredService<TConsumer>(), ServiceLifetime.Singleton));
                implementsAny = true;
            }

            if (typeof(INetSecurityTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(INetSecurityTelemetryConsumer), provider => provider.GetRequiredService<TConsumer>(), ServiceLifetime.Singleton));
                implementsAny = true;
            }

            if (typeof(ISocketsTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
            {
                services.TryAddEnumerable(new ServiceDescriptor(typeof(ISocketsTelemetryConsumer), provider => provider.GetRequiredService<TConsumer>(), ServiceLifetime.Singleton));
                implementsAny = true;
            }
#endif

            if (!implementsAny)
            {
                throw new ArgumentException("TConsumer must implement at least one I*TelemetryConsumer interface.", nameof(TConsumer));
            }

            services.TryAddSingleton<TConsumer>();

            services.AddTelemetryListeners();

            return services;
        }
    }
}
