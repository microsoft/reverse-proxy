// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.CustomResources
{
    public class CustomResourceDefinitionUpdater<TResource> : IHostedService
    {
        private readonly IKubernetes _client;
        private readonly ICustomResourceDefinitionGenerator _generator;
        private readonly CustomResourceDefinitionUpdaterOptions<TResource> _options;

        public CustomResourceDefinitionUpdater(
            IKubernetes client,
            ICustomResourceDefinitionGenerator generator,
            IOptions<CustomResourceDefinitionUpdaterOptions<TResource>> options)
        {
            if (options is null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            _client = client ?? throw new ArgumentNullException(nameof(client));
            _generator = generator ?? throw new ArgumentNullException(nameof(generator));
            _options = options.Value;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var scope = _options.Scope ?? "Namespaced";

            var crd = await _generator.GenerateCustomResourceDefinitionAsync<TResource>(scope, cancellationToken);

            await CreateOrReplaceCustomResourceDefinitionAsync(crd, cancellationToken);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async Task<V1CustomResourceDefinition> CreateOrReplaceCustomResourceDefinitionAsync(
            V1CustomResourceDefinition customResourceDefinition,
            CancellationToken cancellationToken)
        {
            // TODO: log messages from here

            if (customResourceDefinition is null)
            {
                throw new ArgumentNullException(nameof(customResourceDefinition));
            }

            var existingList = await _client.ListCustomResourceDefinitionAsync(
                fieldSelector: $"metadata.name={customResourceDefinition.Name()}",
                cancellationToken: cancellationToken);

            var existingCustomResourceDefinition = existingList?.Items?.SingleOrDefault();

            if (existingCustomResourceDefinition != null)
            {
                customResourceDefinition.Metadata.ResourceVersion = existingCustomResourceDefinition.ResourceVersion();

                return await _client.ReplaceCustomResourceDefinitionAsync(
                    customResourceDefinition,
                    customResourceDefinition.Name(),
                    cancellationToken: cancellationToken);
            }
            else
            {
                return await _client.CreateCustomResourceDefinitionAsync(
                    customResourceDefinition,
                    cancellationToken: cancellationToken);
            }
        }
    }
}
