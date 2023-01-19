// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using System.Collections.Generic;

namespace Yarp.Kubernetes.Tests.TestCluster;

public class TestClusterOptions
{
    public IList<IKubernetesObject<V1ObjectMeta>> InitialResources { get; } = new List<IKubernetesObject<V1ObjectMeta>>();
}
