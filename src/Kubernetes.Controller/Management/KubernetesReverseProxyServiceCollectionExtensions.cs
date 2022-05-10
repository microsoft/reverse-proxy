// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using k8s.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Kubernetes.Controller.Informers;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Configuration;
using Yarp.Kubernetes.Controller.Controllers;
using Yarp.Kubernetes.Controller.Dispatching;
using Yarp.Kubernetes.Controller.Protocol;
using Yarp.Kubernetes.Controller.Services;
using Yarp.ReverseProxy.Configuration;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extensions for <see cref="IServiceCollection"/>
/// used to register the Kubernetes-based ReverseProxy's components.
/// </summary>
public static class KubernetesReverseProxyServiceCollectionExtensions
{
    /// <summary>
    /// Adds ReverseProxy's services to Dependency Injection.
    /// </summary>
    /// <param name="services">Dependency injection registration.</param>
    /// <param name="config">Application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IReverseProxyBuilder AddKubernetesReverseProxy(this IServiceCollection services, IConfiguration config)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        // Add components from the kubernetes controller framework
        services.AddKubernetesControllerRuntime();

        // Add components implemented by this application
        services.AddHostedService<IngressController>();
        services.AddSingleton<ICache, IngressCache>();
        services.AddTransient<IReconciler, Reconciler>();
        services.Configure<YarpOptions>(config.GetSection("Yarp"));

        var provider = new KubernetesConfigProvider();
        services.AddSingleton<IProxyConfigProvider>(provider);
        services.AddSingleton<IUpdateConfig>(provider);

        // Register the necessary Kubernetes resource informers
        services.RegisterResourceInformer<V1Ingress, V1IngressResourceInformer>();
        services.RegisterResourceInformer<V1Service, V1ServiceResourceInformer>();
        services.RegisterResourceInformer<V1Endpoints, V1EndpointsResourceInformer>();
        services.RegisterResourceInformer<V1IngressClass, V1IngressClassResourceInformer>();

        return services.AddReverseProxy();
    }

    /// <summary>
    /// Adds an ingress controller that monitors for Ingress resource changes and notifies a Yarp "Ingress" application.
    /// </summary>
    /// <param name="services">Dependency injection registration.</param>
    /// <param name="config">Application configuration.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddKubernetesIngressMonitor(this IServiceCollection services, IConfiguration config)
    {
        if (config is null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        // Add components from the kubernetes controller framework
        services.AddKubernetesControllerRuntime();

        // Add components implemented by this application
        services.AddHostedService<IngressController>();
        services.AddSingleton<ICache, IngressCache>();
        services.AddTransient<IReconciler, Reconciler>();
        services.AddSingleton<IDispatcher, Dispatcher>();
        services.Configure<YarpOptions>(config.GetSection("Yarp"));

        services.AddSingleton<IUpdateConfig, DispatchConfigProvider>();

        // Register the necessary Kubernetes resource informers
        services.RegisterResourceInformer<V1Ingress, V1IngressResourceInformer>();
        services.RegisterResourceInformer<V1Service, V1ServiceResourceInformer>();
        services.RegisterResourceInformer<V1Endpoints, V1EndpointsResourceInformer>();
        services.RegisterResourceInformer<V1IngressClass, V1IngressClassResourceInformer>();

        return services;
    }

    /// <summary>
    /// Adds the dispatching controller that allows a Yarp "Ingress" application to monitor for changes.
    /// </summary>
    /// <param name="builder">The MVC builder.</param>
    /// <returns>Rhe same <see cref="IMvcBuilder"/> for chaining.</returns>
    public static IMvcBuilder AddKubernetesDispatchController(this IMvcBuilder builder)
    {
        return builder.AddApplicationPart(typeof(DispatchController).Assembly);
    }
}
