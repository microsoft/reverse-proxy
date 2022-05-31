// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Linq;
using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Xunit.Abstractions;
using Yarp.Kubernetes.Controller;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Tests.Utils;

namespace Yarp.Kubernetes.Tests;

public class IngressCacheTests
{
    private readonly Mock<IOptions<YarpOptions>> _mockOptions;
    private readonly IngressCache _cacheUnderTest;

    public IngressCacheTests(ITestOutputHelper output)
    {
        var logger = new TestLogger<IngressCache>(output);
        _mockOptions = new Mock<IOptions<YarpOptions>>();
        _mockOptions.SetupGet(o => o.Value).Returns(new YarpOptions { ControllerClass = "microsoft.com/ingress-yarp" });

        _cacheUnderTest = new IngressCache(_mockOptions.Object, logger);
    }

    [Theory]
    [InlineData("yarp", "microsoft.com/ingress-yarp", false, 1)]
    [InlineData("yarp", "microsoft.com/ingress-yarp", true, 1)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", false, 0)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", true, 0)]
    [InlineData(null, null, false, 0)]
    public void IngressWithClassAnnotationTests(string ingressClassName, string controllerName, bool? isDefault, int expectedIngressCount)
    {
        // Arrange
        if (controllerName is not null)
        {
            var ingressClass = KubeResourceGenerator.CreateIngressClass(ingressClassName, controllerName, isDefault);
            _cacheUnderTest.Update(WatchEventType.Added, ingressClass);
        }

        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");

        // Act
        var change = _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();

        Assert.Equal(expectedIngressCount != 0, change);
        Assert.Equal(expectedIngressCount, ingresses.Length);
    }


    [Theory]
    [InlineData("yarp", "microsoft.com/ingress-yarp", true, 1)]
    [InlineData("yarp", "microsoft.com/ingress-yarp", false, 0)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", false, 0)]
    [InlineData("yarp-internal", "microsoft.com/ingress-yarp-internal", true, 0)]
    [InlineData(null, null, false, 0)]
    public void IngressWithoutClassAnnotationTests(string ingressClassName, string controllerName, bool? isDefault, int expectedIngressCount)
    {
        // Arrange
        if (controllerName is not null)
        {
            var ingressClass = KubeResourceGenerator.CreateIngressClass(ingressClassName, controllerName, isDefault);
            _cacheUnderTest.Update(WatchEventType.Added, ingressClass);
        }

        var ingress = KubeResourceGenerator.CreateIngress("ingress-without-class", "ns-test", null);

        // Act
        var change = _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();

        Assert.Equal(expectedIngressCount != 0, change);
        Assert.Equal(expectedIngressCount, ingresses.Length);
    }

    [Fact]
    public void IngressModifiedToRemoveClass()
    {
        // Arrange
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", false);
        _cacheUnderTest.Update(WatchEventType.Added, ingressClass);

        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Act
        ingress.Spec.IngressClassName = null;
        _cacheUnderTest.Update(WatchEventType.Modified, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();
        Assert.Empty(ingresses);
    }

    [Fact]
    public void IngressClassModifiedToAddDefault()
    {
        // Arrange
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", false);
        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");

        _cacheUnderTest.Update(WatchEventType.Added, ingressClass);

        // Act
        ingressClass.Metadata.Annotations.Add("ingressclass.kubernetes.io/is-default-class", "true");

        _cacheUnderTest.Update(WatchEventType.Modified, ingressClass);
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();
        Assert.Single(ingresses);
    }

    [Fact]
    public void IngressClassDeleted()
    {
        // Arrange
        var ingressClass = KubeResourceGenerator.CreateIngressClass("yarp", "microsoft.com/ingress-yarp", true);
        var ingress = KubeResourceGenerator.CreateIngress("ingress-with-class", "ns-test", "yarp");

        _cacheUnderTest.Update(WatchEventType.Added, ingressClass);

        // Act
        _cacheUnderTest.Update(WatchEventType.Deleted, ingressClass);
        _cacheUnderTest.Update(WatchEventType.Added, ingress);

        // Assert
        var ingresses = _cacheUnderTest.GetIngresses().ToArray();
        Assert.Empty(ingresses);
    }
}
