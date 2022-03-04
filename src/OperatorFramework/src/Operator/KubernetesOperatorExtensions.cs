// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Options;
using Microsoft.Kubernetes.Operator;
using Microsoft.Kubernetes.Operator.Caches;
using Microsoft.Kubernetes.Operator.Reconcilers;
using System;
using System.Linq;

namespace Microsoft.Extensions.DependencyInjection;

public static class KubernetesOperatorExtensions
{
    public static IServiceCollection AddKubernetesOperatorRuntime(this IServiceCollection services)
    {
        services = services.AddKubernetesControllerRuntime();

        if (!services.Any(services => services.ServiceType == typeof(IOperatorHandler<>)))
        {
            services = services.AddSingleton(typeof(IOperatorHandler<>), typeof(OperatorHandler<>));
        }

        if (!services.Any(services => services.ServiceType == typeof(IOperatorCache<>)))
        {
            services = services.AddSingleton(typeof(IOperatorCache<>), typeof(OperatorCache<>));
        }

        if (!services.Any(services => services.ServiceType == typeof(IOperatorReconciler<>)))
        {
            services = services.AddSingleton(typeof(IOperatorReconciler<>), typeof(OperatorReconciler<>));
        }

        return services;
    }

    public static OperatorServiceCollectionBuilder<TResource> AddOperator<TResource>(this IServiceCollection services)
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services = services
            .AddKubernetesOperatorRuntime()
            .AddHostedService<OperatorHostedService<TResource>>()
            .RegisterOperatorResourceInformer<TResource, TResource>();

        return new OperatorServiceCollectionBuilder<TResource>(services);
    }

    public static IServiceCollection AddOperator<TResource>(this IServiceCollection services, Action<OperatorServiceCollectionBuilder<TResource>> configure)
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        if (configure is null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var operatorServices = services.AddOperator<TResource>();
        configure(operatorServices);
        return operatorServices.Services;
    }

    public static IServiceCollection RegisterOperatorResourceInformer<TOperatorResource, TRelatedResource>(this IServiceCollection services)
        where TRelatedResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        return services
            .RegisterResourceInformer<TRelatedResource>()
            .AddTransient<IConfigureOptions<OperatorOptions>, ConfigureOperatorOptions<TOperatorResource, TRelatedResource>>();
    }
}
