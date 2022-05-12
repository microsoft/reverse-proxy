// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Kubernetes.Controller.Informers;

namespace Microsoft.Extensions.DependencyInjection;

public static class KubernetesControllerExtensions
{
    public static IServiceCollection AddKubernetesControllerRuntime(this IServiceCollection services)
    {
        return services.AddKubernetesCore();
    }

    /// <summary>
    /// Registers the resource informer.
    /// </summary>
    /// <typeparam name="TResource">The type of the t related resource.</typeparam>
    /// <typeparam name="TService">The implementation type of the resource informer.</typeparam>
    /// <param name="services">The services.</param>
    /// <returns>IServiceCollection.</returns>
    public static IServiceCollection RegisterResourceInformer<TResource, TService>(this IServiceCollection services)
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
        where TService : IResourceInformer<TResource>
    {
        services.AddSingleton(typeof(IResourceInformer<TResource>), typeof(TService));

        return services
            .RegisterHostedService<IResourceInformer<TResource>>();
    }
}
