// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Http.Features;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Controller.Hosting.Fakes
{
    public sealed class FakeServer : IServer
    {
        public IFeatureCollection Features { get; } = new FeatureCollection();

        public void Dispose()
        {
        }

        public Task StartAsync<TContext>(IHttpApplication<TContext> application, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
