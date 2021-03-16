// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Http;
using Microsoft.Kubernetes.Testing.Models;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Testing
{
    public interface ITestCluster
    {
        Task UnhandledRequest(HttpContext context);

        Task<ListResult> ListResourcesAsync(string group, string version, string plural, ListParameters parameters);
    }
}
