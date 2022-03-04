// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kubernetes.Controller.Informers;
using Microsoft.Kubernetes.Controller.Queues;
using Microsoft.Kubernetes.Operator.Caches;
using Microsoft.Kubernetes.Operator.Reconcilers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Kubernetes.Operator;

public class OperatorHandler<TResource> : IOperatorHandler<TResource>
    where TResource : class, IKubernetesObject<V1ObjectMeta>, new()
{
    private static GroupApiVersionKind _names = GroupApiVersionKind.From<TResource>();
    private readonly List<IResourceInformerRegistration> _registrations = new List<IResourceInformerRegistration>();
    private readonly IRateLimitingQueue<NamespacedName> _queue;
    private readonly IOperatorCache<TResource> _cache;
    private readonly IOperatorReconciler<TResource> _reconciler;
    private readonly ILogger<OperatorHandler<TResource>> _logger;
    private bool _disposedValue;

    public OperatorHandler(
        IOptionsSnapshot<OperatorOptions> optionsProvider,
        IOperatorCache<TResource> cache,
        IOperatorReconciler<TResource> reconciler,
        ILogger<OperatorHandler<TResource>> logger)
    {
        if (optionsProvider is null)
        {
            throw new ArgumentNullException(nameof(optionsProvider));
        }

        var options = optionsProvider.Get(_names.PluralNameGroup);

        foreach (var informer in options.Informers)
        {
            _registrations.Add(informer.Register(Notification));
        }

        var rateLimiter = options.NewRateLimiter();
        _queue = options.NewRateLimitingQueue(rateLimiter);
        _cache = cache;
        _reconciler = reconciler;
        _logger = logger;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing)
            {
                foreach (var registration in _registrations)
                {
                    registration.Dispose();
                }
            }

            _disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }

    private void Notification(WatchEventType eventType, IKubernetesObject<V1ObjectMeta> resource)
    {
        if (resource is TResource customResource)
        {
            OnPrimaryResourceWatchEvent(eventType, customResource);
        }
        else
        {
            OnRelatedResourceWatchEvent(eventType, resource);
        }
    }

    private void OnPrimaryResourceWatchEvent(WatchEventType watchEventType, TResource resource)
    {
        var key = NamespacedName.From(resource);

        _cache.UpdateWorkItem(key, workItem =>
        {
            if (watchEventType == WatchEventType.Added || watchEventType == WatchEventType.Modified)
            {
                workItem = workItem.SetResource(resource);
            }
            else if (watchEventType == WatchEventType.Deleted)
            {
                workItem = workItem.SetResource(default);
            }

            _queue.Add(key);
            return workItem;
        });
    }

    private void OnRelatedResourceWatchEvent(WatchEventType watchEventType, IKubernetesObject<V1ObjectMeta> resource)
    {
        // Check each owner reference on the notified resource
        foreach (var ownerReference in resource.OwnerReferences() ?? Enumerable.Empty<V1OwnerReference>())
        {
            // If this operator's resource type is an owner
            if (string.Equals(ownerReference.Kind, _names.Kind, StringComparison.Ordinal) &&
                string.Equals(ownerReference.ApiVersion, _names.GroupApiVersion, StringComparison.Ordinal))
            {
                // Then hold on to the resource's current state under the owner's cache entry

                var resourceKey = new NamespacedName(
                    @namespace: resource.Namespace(),
                    name: ownerReference.Name);

                var relatedKey = GroupKindNamespacedName.From(resource);

                _cache.UpdateWorkItem(resourceKey, workItem =>
                {
                    if (watchEventType == WatchEventType.Added || watchEventType == WatchEventType.Modified)
                    {
                        workItem = workItem.SetRelated(workItem.Related.SetItem(relatedKey, resource));
                    }
                    else if (watchEventType == WatchEventType.Deleted)
                    {
                        workItem = workItem.SetRelated(workItem.Related.Remove(relatedKey));
                    }

                    _queue.Add(resourceKey);
                    return workItem;
                });
            }
        }
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(new EventId(1, "WaitingForInformers"), "Waiting for resource informers to finish synchronizing.");
        foreach (var registration in _registrations)
        {
            await registration.ReadyAsync(cancellationToken);
        }
        _logger.LogInformation(new EventId(2, "InformersReady"), "All resource informers are ready.");

        while (await ProcessNextWorkItemAsync(cancellationToken))
        {
            // loop until complete
        }
    }

    /// <summary>
    /// processNextWorkItem will read a single work item off the workqueue and attempt to process it, by calling the reconcileHandler.
    /// </summary>
    private async Task<bool> ProcessNextWorkItemAsync(CancellationToken cancellationToken)
    {
        // pkg\internal\controller\controller.go:194
        if (cancellationToken.IsCancellationRequested)
        {
            // Stop working
            return false;
        }

        var (key, shutdown) = await _queue.GetAsync(cancellationToken);
        if (shutdown || cancellationToken.IsCancellationRequested)
        {
            // Stop working
            return false;
        }

        try
        {
            return await ReconcileWorkItemAsync(key, cancellationToken);
        }
        finally
        {
            // We call Done here so the workqueue knows we have finished
            // processing this item. We also must remember to call Forget if we
            // do not want this work item being re-queued. For example, we do
            // not call Forget if a transient error occurs, instead the item is
            // put back on the workqueue and attempted again after a back-off
            // period.
            _queue.Done(key);
        }
    }

    private async Task<bool> ReconcileWorkItemAsync(NamespacedName key, CancellationToken cancellationToken)
    {
        // pkg\internal\controller\controller.go:194
        if (!_cache.TryGetWorkItem(key, out var workItem))
        {
            // no knowledge of this resource at all. forget it ever happened.
            _queue.Forget(key);
            return true;
        }

        ReconcileResult result;
        try
        {
            result = await _reconciler.ReconcileAsync(
                new ReconcileParameters<TResource>(workItem.Resource, workItem.Related),
                cancellationToken);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception error)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            result = new ReconcileResult
            {
                Error = error
            };
        }

        if (result.Error != null)
        {
            // TODO: LOG Reconciler error

            _logger.LogInformation(
                new EventId(3, "ErrorRetry"),
                "Scheduling retry for {ItemName}.{ItemNamespace}: {ErrorMessage}",
                key.Name,
                key.Namespace,
                result.Error.Message);

            _queue.AddRateLimited(key);
            return true;
        }
        else if (result.RequeueAfter > TimeSpan.Zero)
        {
            _logger.LogInformation(
                new EventId(4, "DelayRetry"),
                "Scheduling retry in {DelayTime} for {ItemName}.{ItemNamespace}",
                result.RequeueAfter,
                key.Name,
                key.Namespace);

            _queue.Forget(key);
            _queue.AddAfter(key, result.RequeueAfter);

            // TODO: COUNTER
            return true;
        }
        else if (result.Requeue)
        {
            _logger.LogInformation(
                   new EventId(5, "BackoffRetry"),
                   "Scheduling backoff retry for {ItemName}.{ItemNamespace}",
                   key.Name,
                   key.Namespace);

            _queue.AddRateLimited(key);
            return true;
        }

        _queue.Forget(key);
        return true;
    }
}
