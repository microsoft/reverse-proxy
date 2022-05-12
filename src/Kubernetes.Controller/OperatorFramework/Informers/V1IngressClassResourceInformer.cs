// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Informers;

internal class V1IngressClassResourceInformer : ResourceInformer<V1IngressClass, V1IngressClassList>
{
    public V1IngressClassResourceInformer(
        IKubernetes client,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1IngressClassResourceInformer> logger)
        : base(client, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1IngressClassList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, CancellationToken cancellationToken = default)
    {
        return Client.ListIngressClassWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, cancellationToken: cancellationToken); 
    }
}
