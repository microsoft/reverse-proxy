// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.KubeConfigModels;
using Microsoft.Extensions.Hosting;

namespace Yarp.Kubernetes.Tests.TestCluster;

public interface ITestClusterHost : IHost
{
    K8SConfiguration KubeConfig { get; }

    IKubernetes Client { get; }

    ITestCluster Cluster { get; }
}
