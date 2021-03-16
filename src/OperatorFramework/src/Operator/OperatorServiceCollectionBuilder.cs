// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Kubernetes;
using Microsoft.Kubernetes.Operator;
using Microsoft.Kubernetes.Operator.Generators;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Class OperatorServiceCollection.
    /// </summary>
    /// <typeparam name="TOperatorResource">The type of the t resource.</typeparam>
    public class OperatorServiceCollectionBuilder<TOperatorResource>
            where TOperatorResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        private IServiceCollection _services;

        /// <summary>
        /// Initializes a new instance of the <see cref="OperatorServiceCollectionBuilder{TResource}" /> class.
        /// </summary>
        /// <param name="services">The services.</param>
        public OperatorServiceCollectionBuilder(IServiceCollection services)
        {
            _services = services;
        }

        /// <summary>
        /// Gets or sets the services.
        /// </summary>
        /// <value>The services.</value>
        public IServiceCollection Services => _services;

        /// <summary>
        /// Withes the related resource.
        /// </summary>
        /// <typeparam name="TRelatedResource">The type of the t related resource.</typeparam>
        /// <returns>OperatorServiceCollection&lt;TResource&gt;.</returns>
        public OperatorServiceCollectionBuilder<TOperatorResource> WithRelatedResource<TRelatedResource>()
            where TRelatedResource : class, IKubernetesObject<V1ObjectMeta>, new()
        {
            _services = _services.RegisterOperatorResourceInformer<TOperatorResource, TRelatedResource>();
            return this;
        }

        public OperatorServiceCollectionBuilder<TOperatorResource> WithGenerator<TGenerator>()
            where TGenerator : class, IOperatorGenerator<TOperatorResource>
        {
            _services = _services.AddTransient<IOperatorGenerator<TOperatorResource>, TGenerator>();
            return this;
        }

        public OperatorServiceCollectionBuilder<TOperatorResource> Configure(Action<OperatorOptions> configureOptions)
        {
            var names = GroupApiVersionKind.From<TOperatorResource>();
            _services = _services.Configure(names.PluralNameGroup, configureOptions);
            return this;
        }
    }
}
