// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes.Controller.Hosting;
using Microsoft.Kubernetes.Controller.Informers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PodStatusConditions
{
    public class StatusController : BackgroundHostedService
    {
        private readonly IEnumerable<IResourceInformerRegistration> _informers;

        public StatusController(
            IResourceInformer<V1Pod> podInformer,
            IResourceInformer<V1ConfigMap> configMapInformer,
            IHostApplicationLifetime hostApplicationLifetime,
            ILogger<StatusController> logger)
            : base(hostApplicationLifetime, logger)
        {
            if (hostApplicationLifetime is null)
            {
                throw new ArgumentNullException(nameof(hostApplicationLifetime));
            }

            if (podInformer is null)
            {
                throw new ArgumentNullException(nameof(podInformer));
            }

            if (configMapInformer is null)
            {
                throw new ArgumentNullException(nameof(configMapInformer));
            }

            if (logger is null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            _informers = new[]
            {
                podInformer.Register(Notification),
                configMapInformer.Register(Notification),
            };
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var informer in _informers)
                {
                    informer?.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        private void Notification(WatchEventType eventType, V1ConfigMap configMap)
        {
        }

        private void Notification(WatchEventType eventType, V1Pod pod)
        {
        }

        public override async Task RunAsync(CancellationToken cancellationToken)
        {
            foreach (var informer in _informers)
            {
                await informer.ReadyAsync(cancellationToken);
            }

            await Task.Delay(int.MaxValue, cancellationToken);
        }
    }
}
