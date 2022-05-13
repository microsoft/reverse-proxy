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

internal class V1ServiceResourceInformer : ResourceInformer<V1Service, V1ServiceList>
{
    public V1ServiceResourceInformer(
        IKubernetes client,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1ServiceResourceInformer> logger)
        : base(client, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1ServiceList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, CancellationToken cancellationToken = default)
    {
        return Client.ListServiceForAllNamespacesWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, cancellationToken: cancellationToken); 
    }
}
