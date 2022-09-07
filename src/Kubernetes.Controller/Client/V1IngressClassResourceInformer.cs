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

internal class V1IngressClassResourceInformer : ResourceInformer<V1IngressClass, V1IngressClassList>
{
    public V1IngressClassResourceInformer(
        IKubernetes client,
        ResourceSelector<V1IngressClass> selector,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1IngressClassResourceInformer> logger)
        : base(client, selector, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1IngressClassList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, ResourceSelector<V1IngressClass> resourceSelector = null, CancellationToken cancellationToken = default)
    {
        return Client.ListIngressClassWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, fieldSelector: resourceSelector?.FieldSelector, cancellationToken: cancellationToken); 
    }
}
