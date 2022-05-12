// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes.Controller.Informers;
using Microsoft.Rest;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.OperatorFramework.Informers;

internal class V1PodResourceInformer : ResourceInformer<V1Pod, V1PodList>
{
    public V1PodResourceInformer(
        IKubernetes client,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1PodResourceInformer> logger)
        : base(client, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1PodList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, CancellationToken cancellationToken = default)
    {
        return Client.ListPodForAllNamespacesWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, cancellationToken: cancellationToken);
    }
}
