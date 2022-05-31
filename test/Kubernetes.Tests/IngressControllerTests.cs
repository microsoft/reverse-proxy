// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Client.Tests;
using Yarp.Kubernetes.Controller.Services;
using Yarp.Kubernetes.Tests.Utils;
using Yarp.Tests.Common;

namespace Yarp.Kubernetes.Tests;

public class IngressControllerTests
{
    private readonly Mock<ICache> _mockCache = new();
    private readonly Mock<IReconciler> _mockReconciler = new();
    private readonly SyncResourceInformer<V1Ingress> _ingressInformer = new();
    private readonly SyncResourceInformer<V1Service> _serviceInformer = new();
    private readonly SyncResourceInformer<V1Endpoints> _endpointsInformer = new();
    private readonly SyncResourceInformer<V1IngressClass> _ingressClassInformer = new();
    private readonly Mock<IHostApplicationLifetime> _mockHostApplicationLifetime = new();
    private readonly IngressController _controller;

    public IngressControllerTests(ITestOutputHelper output)
    {
        var logger = new TestLogger<IngressController>(output);
        _controller = new IngressController(_mockCache.Object, _mockReconciler.Object, _ingressInformer, _serviceInformer, _endpointsInformer, _ingressClassInformer, _mockHostApplicationLifetime.Object, logger);
    }

    [Fact]
    public async Task ReconciliationContinuesOnReconcilerError()
    {
        _mockCache.Setup(x => x.Update(It.IsAny<WatchEventType>(), It.IsAny<V1Ingress>())).Returns(true);

        var awaiter = new SemaphoreSlim(0, 1);
        Exception reconcilerError = null;
        _mockReconciler
            .Setup(x => x.ProcessAsync(It.IsAny<CancellationToken>())).Returns(
                (CancellationToken _) =>
                {
                    awaiter.Release();
                    if (reconcilerError != null)
                    {
                        var e = reconcilerError;
                        reconcilerError = null;
                        return Task.FromException(e);
                    }

                    return Task.CompletedTask;
                });

        await _controller.StartAsync(CancellationToken.None).DefaultTimeout();
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Exactly(1));

        _ingressInformer.PublishUpdate(WatchEventType.Added, new V1Ingress());
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        reconcilerError = new Exception("reconicliation failed");
        _ingressInformer.PublishUpdate(WatchEventType.Added, new V1Ingress());
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.AtLeast(3));
        await awaiter.WaitAsync().DefaultTimeout();
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.AtLeast(4));
    }
}
