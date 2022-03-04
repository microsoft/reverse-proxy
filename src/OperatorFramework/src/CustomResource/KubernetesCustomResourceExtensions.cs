// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Kubernetes.CustomResources;
using System;
using System.Linq;

namespace Microsoft.Extensions.DependencyInjection;

public static class KubernetesCustomResourceExtensions
{
    public static IServiceCollection AddKubernetesCustomResources(this IServiceCollection services)
    {
        if (!services.Any(services => services.ServiceType == typeof(ICustomResourceDefinitionGenerator)))
        {
            services = services.AddTransient<ICustomResourceDefinitionGenerator, CustomResourceDefinitionGenerator>();
        }
        return services;
    }

    public static IServiceCollection AddCustomResourceDefinitionUpdater<TResource>(this IServiceCollection services)
    {
        return services
            .AddKubernetesCustomResources()
            .AddHostedService<CustomResourceDefinitionUpdater<TResource>>();
    }

    public static IServiceCollection AddCustomResourceDefinitionUpdater<TResource>(
        this IServiceCollection services,
        Action<CustomResourceDefinitionUpdaterOptions> configure)
    {
        return services
            .AddKubernetesCustomResources()
            .AddHostedService<CustomResourceDefinitionUpdater<TResource>>()
            .Configure<CustomResourceDefinitionUpdaterOptions<TResource>>(options => configure(options));
    }
}
