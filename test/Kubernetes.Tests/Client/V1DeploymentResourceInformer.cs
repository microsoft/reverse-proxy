// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client.Tests;

internal class V1DeploymentResourceInformer : ResourceInformer<V1Deployment, V1DeploymentList>
{
    public V1DeploymentResourceInformer(
        IKubernetes client,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<V1DeploymentResourceInformer> logger)
        : base(client, hostApplicationLifetime, logger)
    {
    }

    protected override Task<HttpOperationResponse<V1DeploymentList>> RetrieveResourceListAsync(bool? watch = null, string resourceVersion = null, CancellationToken cancellationToken = default)
    {
        return Client.ListDeploymentForAllNamespacesWithHttpMessagesAsync(watch: watch, resourceVersion: resourceVersion, cancellationToken: cancellationToken);
    }
}
