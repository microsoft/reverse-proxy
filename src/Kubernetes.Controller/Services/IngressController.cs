// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Kubernetes;
using Microsoft.Kubernetes.Controller.Hosting;
using Microsoft.Kubernetes.Controller.Informers;
using Microsoft.Kubernetes.Controller.Queues;
using Yarp.Kubernetes.Controller.Caching;
using Yarp.Kubernetes.Controller.Dispatching;

namespace Yarp.Kubernetes.Controller.Services;

/// <summary>
/// Controller receives notifications from informers. The data which is needed for processing is
/// saved in a <see cref="ICache"/> instance and resources which need to be reconciled are
/// added to an <see cref="IRateLimitingQueue{QueueItem}"/>. The background task dequeues
/// items and passes them to an <see cref="IReconciler"/> service for processing.
/// </summary>
public class IngressController : BackgroundHostedService
{
    private readonly IResourceInformerRegistration[] _registrations;
    private readonly IWorkQueue<QueueItem> _queue;
    private readonly ICache _cache;
    private readonly IReconciler _reconciler;
    private readonly QueueItem _ingressChangeQueueItem;

    public IngressController(
        ICache cache,
        IReconciler reconciler,
        IResourceInformer<V1Ingress> ingressInformer,
        IResourceInformer<V1Service> serviceInformer,
        IResourceInformer<V1Endpoints> endpointsInformer,
        IHostApplicationLifetime hostApplicationLifetime,
        ILogger<IngressController> logger)
        : base(hostApplicationLifetime, logger)
    {
        if (ingressInformer is null)
        {
            throw new ArgumentNullException(nameof(ingressInformer));
        }

        if (serviceInformer is null)
        {
            throw new ArgumentNullException(nameof(serviceInformer));
        }

        if (endpointsInformer is null)
        {
            throw new ArgumentNullException(nameof(endpointsInformer));
        }

        if (hostApplicationLifetime is null)
        {
            throw new ArgumentNullException(nameof(hostApplicationLifetime));
        }

        if (logger is null)
        {
            throw new ArgumentNullException(nameof(logger));
        }

        _registrations = new[]
        {
            ingressInformer.Register(Notification),
            serviceInformer.Register(Notification),
            endpointsInformer.Register(Notification),
        };

        _queue = new ProcessingRateLimitedQueue<QueueItem>(0.5, 1);

        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _reconciler = reconciler ?? throw new ArgumentNullException(nameof(reconciler));
        _reconciler.OnAttach(TargetAttached);

        _ingressChangeQueueItem = new QueueItem("Ingress Change", null);
    }

    /// <summary>
    /// Disconnects from resource informers, and cause queue to become shut down.
    /// </summary>
    /// <param name="disposing"></param>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            foreach (var registration in _registrations)
            {
                registration.Dispose();
            }

            _queue.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Called each time a new connection arrives on the /api/dispatch endpoint.
    /// All of the currently-known Ingress names are queued up to be sent
    /// to the new target.
    /// </summary>
    /// <param name="target">The interface to target a connected client.</param>
    private void TargetAttached(IDispatchTarget target)
    {
        var keys = new List<NamespacedName>();
        _cache.GetKeys(keys);
        if (keys.Count > 0)
        {
            _queue.Add(new QueueItem("Target Attached", target));
        }
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernets API server.</param>
    private void Notification(WatchEventType eventType, V1Ingress resource)
    {
        _cache.Update(eventType, resource);
        _queue.Add(_ingressChangeQueueItem);
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernets API server.</param>
    private void Notification(WatchEventType eventType, V1Service resource)
    {
        var ingressNames = _cache.Update(eventType, resource);
        if (ingressNames.Count > 0)
        {
            _queue.Add(_ingressChangeQueueItem);
        }
    }

    /// <summary>
    /// Called by the informer with real-time resource updates.
    /// </summary>
    /// <param name="eventType">Indicates if the resource new, updated, or deleted.</param>
    /// <param name="resource">The information as provided by the Kubernets API server.</param>
    private void Notification(WatchEventType eventType, V1Endpoints resource)
    {
        var ingressNames = _cache.Update(eventType, resource);
        if (ingressNames.Count > 0)
        {
            _queue.Add(_ingressChangeQueueItem);
        }
    }

    /// <summary>
    /// Called once at startup by the hosting infrastructure. This function must remain running
    /// for the entire lifetime of an application.
    /// </summary>
    /// <param name="cancellationToken">Indicates when the web application is shutting down.</param>
    /// <returns>The Task representing the async function results.</returns>
    public override async Task RunAsync(CancellationToken cancellationToken)
    {
        // First wait for all informers to fully List resources before processing begins.
        foreach (var registration in _registrations)
        {
            await registration.ReadyAsync(cancellationToken).ConfigureAwait(false);
        }

        // At this point we know that all of the Ingress and Endpoint caches are at least in sync
        // with cluster's state as of the start of this controller.

        // Now begin one loop to process work until an application shudown is requested.
        while (!cancellationToken.IsCancellationRequested)
        {
            // Dequeue the next item to process
            var (item, shutdown) = await _queue.GetAsync(cancellationToken).ConfigureAwait(false);
            if (shutdown)
            {
                return;
            }

            try
            {
                await _reconciler.ProcessAsync(cancellationToken).ConfigureAwait(false);

                // calling Done after GetAsync informs the queue
                // that the item is no longer being actively processed
                _queue.Done(item);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                Logger.LogInformation("Rescheduling {Change}", item.Change);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

                // Any failure to process this item results in being re-queued
                _queue.Add(item);
            }
        }
    }
}
