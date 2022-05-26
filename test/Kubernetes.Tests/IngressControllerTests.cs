// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes;
using Microsoft.Kubernetes.Controller.Informers;
using Microsoft.Kubernetes.Testing;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Services;
using Yarp.Kubernetes.OperatorFramework.Utils;

namespace Yarp.Kubernetes.OperatorFramework.Informers;

public class IngressControllerTests
{
    private readonly Mock<ICache> _mockCache = new();
    private readonly Mock<IReconciler> _mockReconciler = new();
    private readonly SyncResourceInformer<V1Ingress> _ingressInformer = new();
    private readonly SyncResourceInformer<V1Service> _serviceInformer = new();
    private readonly SyncResourceInformer<V1Endpoints> _endpointsInformer = new();
    private readonly SyncResourceInformer<V1IngressClass> _ingressClassInformer = new();
    private readonly Mock<IHostApplicationLifetime> _mockHostApplicationLifetime = new();
    private readonly Mock<ILogger<IngressController>> _mockLogger = new();
    private readonly IngressController controller;

    public IngressControllerTests()
    {
        controller = new IngressController(_mockCache.Object, _mockReconciler.Object, _ingressInformer, _serviceInformer, _endpointsInformer, _ingressClassInformer, _mockHostApplicationLifetime.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task ReconciliationContinuesOnReconcilerError()
    {
        using var cancellation = new CancellationTokenSource(Debugger.IsAttached ? TimeSpan.FromMinutes(5) : TimeSpan.FromSeconds(10));

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

        await controller.StartAsync(cancellation.Token);
        await awaiter.WaitAsync(cancellation.Token);
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Exactly(1));

        _ingressInformer.PublishUpdate(WatchEventType.Added, new V1Ingress());
        await awaiter.WaitAsync(cancellation.Token);
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));

        reconcilerError = new Exception("reconicliation failed");
        _ingressInformer.PublishUpdate(WatchEventType.Added, new V1Ingress());
        await awaiter.WaitAsync(cancellation.Token);
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.AtLeast(3));
        await awaiter.WaitAsync(cancellation.Token);
        _mockReconciler.Verify(x => x.ProcessAsync(It.IsAny<CancellationToken>()), Times.AtLeast(4));
    }
}
