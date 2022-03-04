// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Kubernetes.Resources;
using Xunit;

namespace Microsoft.Kubernetes;

public class KubernetesCoreExtensionsTests
{
    [Fact]
    public void KubernetesClientIsAdded()
    {
        var services = new ServiceCollection();

        services.AddKubernetesCore();

        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IKubernetes>());
    }

    [Fact]
    public void HelperServicesAreAdded()
    {
        var services = new ServiceCollection();

        services.AddKubernetesCore();

        var serviceProvider = services.BuildServiceProvider();
        Assert.NotNull(serviceProvider.GetService<IResourceSerializers>());
    }


    [Fact]
    public void ExistingClientIsNotReplaced()
    {
        using var client = new k8s.Kubernetes(KubernetesClientConfiguration.BuildDefaultConfig());
        var services = new ServiceCollection();

        services.AddSingleton<IKubernetes>(client);
        services.AddKubernetesCore();

        var serviceProvider = services.BuildServiceProvider();
        Assert.Same(client, serviceProvider.GetService<IKubernetes>());
    }
}
