// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Kubernetes.Controller.Informers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Operator
{
    public sealed class FakeResourceInformer<TResource> : IResourceInformer<TResource>, IResourceInformerRegistration
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        public ResourceInformerCallback<TResource> Callback { get; set; } = (_, _) => { };

        public void Dispose()
        {
        }

        public Task ReadyAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public IResourceInformerRegistration Register(ResourceInformerCallback<TResource> callback)
        {
            Callback = callback;
            return this;
        }

        public IResourceInformerRegistration Register(ResourceInformerCallback<IKubernetesObject<V1ObjectMeta>> callback)
        {
            Callback = (eventType, resource) => callback(eventType, resource);
            return this;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
