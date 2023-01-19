// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;
using Yarp.Kubernetes.Tests.TestCluster.Models;

namespace Yarp.Kubernetes.Tests.TestCluster;

public interface ITestCluster
{
    Task UnhandledRequest(HttpContext context);

    Task<ListResult> ListResourcesAsync(string group, string version, string plural, ListParameters parameters);
}
