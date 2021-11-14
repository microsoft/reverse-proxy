// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Yarp.Telemetry.Consumption;

namespace Microsoft.Extensions.DependencyInjection;

public static class TelemetryConsumptionExtensions
{
#if NET
    /// <summary>
    /// Registers all telemetry listeners (Forwarder, Kestrel, Http, NameResolution, NetSecurity, Sockets and WebSockets).
    /// </summary>
#else
    /// <summary>
    /// Registers all telemetry listeners (Forwarder, Kestrel and WebSockets).
    /// </summary>
#endif
    public static IServiceCollection AddTelemetryListeners(this IServiceCollection services)
    {
        services.AddHostedService<WebSocketsEventListenerService>();
        services.AddHostedService<ForwarderEventListenerService>();
        services.AddHostedService<KestrelEventListenerService>();
#if NET
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

        if (consumer is IWebSocketsTelemetryConsumer webSocketsTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(webSocketsTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is IForwarderTelemetryConsumer forwarderTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(forwarderTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is IKestrelTelemetryConsumer kestrelTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(kestrelTelemetryConsumer));
            implementsAny = true;
        }

#if NET
        if (consumer is IHttpTelemetryConsumer httpTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(httpTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is INameResolutionTelemetryConsumer nameResolutionTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(nameResolutionTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is INetSecurityTelemetryConsumer netSecurityTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(netSecurityTelemetryConsumer));
            implementsAny = true;
        }

        if (consumer is ISocketsTelemetryConsumer socketsTelemetryConsumer)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton(socketsTelemetryConsumer));
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

        if (typeof(IWebSocketsTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IWebSocketsTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IForwarderTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IForwarderTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(IKestrelTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IKestrelTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

#if NET
        if (typeof(IHttpTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (IHttpTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(INameResolutionTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (INameResolutionTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(INetSecurityTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (INetSecurityTelemetryConsumer)services.GetRequiredService<TConsumer>());
            implementsAny = true;
        }

        if (typeof(ISocketsTelemetryConsumer).IsAssignableFrom(typeof(TConsumer)))
        {
            services.AddSingleton(services => (ISocketsTelemetryConsumer)services.GetRequiredService<TConsumer>());
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
