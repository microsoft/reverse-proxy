// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes.Controller.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Operator
{
    public class OperatorHostedService<TResource> : BackgroundHostedService
        where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly IOperatorHandler<TResource> _handler;

        public OperatorHostedService(
            IOperatorHandler<TResource> handler,
            IHostApplicationLifetime hostApplicationLifetime,
            ILogger<OperatorHostedService<TResource>> logger)
            : base(hostApplicationLifetime, logger)
        {
            _handler = handler;
        }

        public override Task RunAsync(CancellationToken cancellationToken)
        {
            return _handler.RunAsync(cancellationToken);
        }
    }
}
