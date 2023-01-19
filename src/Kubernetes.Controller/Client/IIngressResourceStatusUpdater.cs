// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.Kubernetes.Controller.Client;

public interface IIngressResourceStatusUpdater
{
    /// <summary>
    /// Updates the status of cached ingresses.
    /// </summary>
    Task UpdateStatusAsync();
}
