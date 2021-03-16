// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Hosting;
using Microsoft.Kubernetes.Controller.Hosting;
using System.Linq;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Class ServiceCollectionHostedServiceAdapterExtensions.
    /// </summary>
    public static class ServiceCollectionHostedServiceAdapterExtensions
    {
        /// <summary>
        /// Registers the hosted service.
        /// </summary>
        /// <typeparam name="TService">The type of the t service.</typeparam>
        /// <param name="services">The services.</param>
        /// <returns>IServiceCollection.</returns>
        public static IServiceCollection RegisterHostedService<TService>(this IServiceCollection services)
            where TService : IHostedService
        {
            if (!services.Any(serviceDescriptor => serviceDescriptor.ServiceType == typeof(HostedServiceAdapter<TService>)))
            {
                services = services.AddHostedService<HostedServiceAdapter<TService>>();
            }

            return services;
        }
    }
}
