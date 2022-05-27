// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;

namespace Yarp.Kubernetes.Controller.Client;

/// <summary>
/// Class KubernetesClientOptions.
/// </summary>
public class KubernetesClientOptions
{
    /// <summary>
    /// Gets or sets the configuration.
    /// </summary>
    /// <value>The configuration.</value>
    public KubernetesClientConfiguration Configuration { get; set; }
}
