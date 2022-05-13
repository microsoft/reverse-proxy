// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

internal class V1IngressResourceInformer : ResourceInformer<V1Ingress, V1IngressList>
{
    public V1IngressResourceInformer(
        IKubernetes client,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1IngressResourceInformer> logger)
        : base(client, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1IngressList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, CancellationToken cancellationToken = default)
    {
        return Client.ListIngressForAllNamespacesWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, cancellationToken: cancellationToken); 
    }
}
