// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Kubernetes.Resources;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace UsingCustomResources
{
    public static class CreateOrReplaceExtensions
    {
        private static readonly IResourceSerializers _serializers = new ResourceSerializers();

        public static async Task<V1CustomResourceDefinition> CreateOrReplaceCustomResourceDefinitionAsync(
            this IKubernetes client,
            V1CustomResourceDefinition customResourceDefinition,
            CancellationToken cancellationToken)
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (customResourceDefinition is null)
            {
                throw new ArgumentNullException(nameof(customResourceDefinition));
            }

            var existingList = await client.ListCustomResourceDefinitionAsync(
                fieldSelector: $"metadata.name={customResourceDefinition.Name()}",
                cancellationToken: cancellationToken);

            var existingCustomResourceDefinition = existingList?.Items?.SingleOrDefault();

            if (existingCustomResourceDefinition != null)
            {
                customResourceDefinition.Metadata.ResourceVersion = existingCustomResourceDefinition.ResourceVersion();

                return await client.ReplaceCustomResourceDefinitionAsync(
                    customResourceDefinition,
                    customResourceDefinition.Name(),
                    cancellationToken: cancellationToken);
            }
            else
            {
                return await client.CreateCustomResourceDefinitionAsync(
                    customResourceDefinition,
                    cancellationToken: cancellationToken);
            }
        }

        public static async Task<TResource> CreateOrReplaceClusterCustomObjectAsync<TResource>(
            this IKubernetes client,
            string group,
            string version,
            string plural,
            TResource resource,
            CancellationToken cancellationToken) where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (string.IsNullOrEmpty(group))
            {
                throw new ArgumentException($"'{nameof(group)}' cannot be null or empty", nameof(group));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException($"'{nameof(version)}' cannot be null or empty", nameof(version));
            }

            if (string.IsNullOrEmpty(plural))
            {
                throw new ArgumentException($"'{nameof(plural)}' cannot be null or empty", nameof(plural));
            }

            if (resource is null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var list = _serializers.Convert<KubernetesList<TResource>>(await client.ListClusterCustomObjectAsync(
                group: group,
                version: version,
                plural: plural,
                fieldSelector: $"metadata.name={resource.Name()}",
                cancellationToken: cancellationToken));

            var resourceExisting = list.Items.SingleOrDefault();

            if (resourceExisting != null)
            {
                resource.Metadata.ResourceVersion = resourceExisting.ResourceVersion();

                return _serializers.Convert<TResource>(await client.ReplaceClusterCustomObjectAsync(
                    body: resource,
                    group: group,
                    version: version,
                    plural: plural,
                    name: resource.Name(),
                    cancellationToken: cancellationToken));
            }
            else
            {
                return _serializers.Convert<TResource>(await client.CreateClusterCustomObjectAsync(
                    body: resource,
                    group: group,
                    version: version,
                    plural: plural,
                    cancellationToken: cancellationToken));
            }
        }

        public static async Task<TResource> CreateOrReplaceNamespacedCustomObjectAsync<TResource>(
            this IKubernetes client,
            string group,
            string version,
            string plural,
            TResource resource,
            CancellationToken cancellationToken) where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
        {
            if (client is null)
            {
                throw new ArgumentNullException(nameof(client));
            }

            if (string.IsNullOrEmpty(group))
            {
                throw new ArgumentException($"'{nameof(group)}' cannot be null or empty", nameof(group));
            }

            if (string.IsNullOrEmpty(version))
            {
                throw new ArgumentException($"'{nameof(version)}' cannot be null or empty", nameof(version));
            }

            if (string.IsNullOrEmpty(plural))
            {
                throw new ArgumentException($"'{nameof(plural)}' cannot be null or empty", nameof(plural));
            }

            if (resource is null)
            {
                throw new ArgumentNullException(nameof(resource));
            }

            var list = _serializers.Convert<KubernetesList<TResource>>(await client.ListNamespacedCustomObjectAsync(
                group: group,
                version: version,
                namespaceParameter: resource.Namespace(),
                plural: plural,
                fieldSelector: $"metadata.name={resource.Name()}",
                cancellationToken: cancellationToken));

            var resourceExisting = list.Items.SingleOrDefault();

            if (resourceExisting != null)
            {
                resource.Metadata.ResourceVersion = resourceExisting.ResourceVersion();

                return _serializers.Convert<TResource>(await client.ReplaceNamespacedCustomObjectAsync(
                    body: resource,
                    group: group,
                    version: version,
                    namespaceParameter: resource.Namespace(),
                    plural: plural,
                    name: resource.Name(),
                    cancellationToken: cancellationToken));
            }
            else
            {
                return _serializers.Convert<TResource>(await client.CreateNamespacedCustomObjectAsync(
                    body: resource,
                    group: group,
                    version: version,
                    namespaceParameter: resource.Namespace(),
                    plural: plural,
                    cancellationToken: cancellationToken));
            }
        }
    }
}
