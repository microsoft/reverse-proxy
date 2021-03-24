// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.CustomResources
{
    /// <summary>
    /// Interface ICustomResourceDefinitionGenerator is an dependency injection service used to
    /// generate a Kubernetes CustomResourceDefinition from a serializable resource class.
    /// </summary>
    public interface ICustomResourceDefinitionGenerator
    {
        /// <summary>
        /// Generates the custom resource definition from the <typeparamref name="TResource"/> class.
        /// The class should be an <see cref="k8s.IKubernetesObject"/> object with ApiVersion, Type, and Metadata properties.
        /// </summary>
        /// <typeparam name="TResource">The type of the resource to generate.</typeparam>
        /// <param name="scope">The scope indicates whether the defined custom resource is cluster- or namespace-scoped. Allowed values are `Cluster` and `Namespaced`.</param>
        /// <returns>The generated V1CustomResourceDefinition instance.</returns>
        Task<V1CustomResourceDefinition> GenerateCustomResourceDefinitionAsync<TResource>(string scope, CancellationToken cancellationToken = default);

        /// <summary>
        /// Generates the custom resource definition from the <typeparamref name="TResource"/> class.
        /// The class should be an <see cref="k8s.IKubernetesObject"/> object with ApiVersion, Type, and Metadata properties.
        /// </summary>
        /// <typeparam name="TResource">The type of the resource to generate.</typeparam>
        /// <param name="scope">The scope indicates whether the defined custom resource is cluster- or namespace-scoped. Allowed values are `Cluster` and `Namespaced`.</param>
        /// <returns>The generated V1CustomResourceDefinition instance.</returns>
        Task<V1CustomResourceDefinition> GenerateCustomResourceDefinitionAsync(Type resourceType, string scope, CancellationToken cancellationToken = default);
    }
}
