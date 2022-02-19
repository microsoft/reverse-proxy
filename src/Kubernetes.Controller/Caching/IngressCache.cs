// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Kubernetes;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Yarp.Kubernetes.Controller.Services;

namespace Yarp.Kubernetes.Controller.Caching;

/// <summary>
/// ICache service interface holds onto least amount of data necessary
/// for <see cref="IReconciler"/> to process work.
/// </summary>
public class IngressCache : ICache
{
    private readonly object _sync = new object();
    private readonly Dictionary<string, IngressClassData> _ingressClassData = new Dictionary<string, IngressClassData>();
    private readonly Dictionary<string, NamespaceCache> _namespaceCaches = new Dictionary<string, NamespaceCache>();
    private readonly YarpOptions _options;
    private readonly IServerCertificateCache _certificateCache;
    private readonly ILogger<IngressCache> _logger;

    private bool _isDefaultController;

    public IngressCache(IOptions<YarpOptions> options, IServerCertificateCache certificateCache, ILogger<IngressCache> logger)
    {
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _certificateCache = certificateCache ?? throw new ArgumentNullException(nameof(certificateCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void Update(WatchEventType eventType, V1IngressClass ingressClass)
    {
        if (ingressClass is null)
        {
            throw new ArgumentNullException(nameof(ingressClass));
        }

        _logger.LogDebug($"Found ingress class {ingressClass.Name()} of type {ingressClass.Spec.Controller}");

        if (!string.Equals(_options.ControllerClass, ingressClass.Spec.Controller, StringComparison.OrdinalIgnoreCase))
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            _logger.LogInformation(
                "Ignoring {IngressClassNamespace}/{IngressClassName} as the spec.controller is not the same as this ingress ({ControllerClass})",
                ingressClass.Metadata.NamespaceProperty,
                ingressClass.Metadata.Name,
                _options.ControllerClass);
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            return;
        }

        var ingressClassName = ingressClass.Name();
        lock (_sync)
        {
            if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
            {
                _ingressClassData[ingressClassName] = new IngressClassData(ingressClass);
            }
            else if (eventType == WatchEventType.Deleted)
            {
                _ingressClassData.Remove(ingressClassName);
            }

            _isDefaultController = _ingressClassData.Values.Any(ic => ic.IsDefault);
        }
    }

    public bool Update(WatchEventType eventType, V1Ingress ingress)
    {
        if (ingress is null)
        {
            throw new ArgumentNullException(nameof(ingress));
        }

        _logger.LogDebug($"Found ingress {ingress.Namespace()}/{ingress.Name()} of class {ingress.Spec.IngressClassName}");

        if (IsYarpIngress(ingress.Spec))
        {
            Namespace(ingress.Namespace()).Update(eventType, ingress);
            return true;
        }

#pragma warning disable CA1303 // Do not pass literals as localized parameters
        if (eventType == WatchEventType.Modified && Namespace(ingress.Namespace()).IngressExists(ingress))
        {
            // Special handling for an ingress that has the ingressClassName removed
            _logger.LogInformation("Removing ingress {IngressNamespace}/{IngressName} because of unknown ingress class {IngressClassName}", ingress.Metadata.NamespaceProperty, ingress.Metadata.Name, ingress.Spec.IngressClassName);
            Namespace(ingress.Namespace()).Update(WatchEventType.Deleted, ingress);
            return true;
        }

        _logger.LogInformation("Ignoring ingress {IngressNamespace}/{IngressName} because of ingress class {IngressClassName}", ingress.Metadata.NamespaceProperty, ingress.Metadata.Name, ingress.Spec.IngressClassName);
#pragma warning restore CA1303 // Do not pass literals as localized parameters

        return false;
    }

    public ImmutableList<string> Update(WatchEventType eventType, V1Service service)
    {
        if (service is null)
        {
            throw new ArgumentNullException(nameof(service));
        }

        return Namespace(service.Namespace()).Update(eventType, service);
    }

    public ImmutableList<string> Update(WatchEventType eventType, V1Endpoints endpoints)
    {
        return Namespace(endpoints.Namespace()).Update(eventType, endpoints);
    }

    public void Update(WatchEventType eventType, V1Secret secret)
    {
#if NET5_0_OR_GREATER
#pragma warning disable CA1308 // Normalize strings to uppercase
        var key = $"{secret.Namespace()}/{secret.Name()}".ToLowerInvariant();
#pragma warning restore CA1308 // Normalize strings to uppercase

        if (eventType == WatchEventType.Added || eventType == WatchEventType.Modified)
        {
            _certificateCache.UpdateSecret(key, secret);
        }
        else if (eventType == WatchEventType.Deleted)
        {
            var ingressWithoutSecret = false;

            lock (_sync)
            {
                _certificateCache.RemoveSecret(key);

                // Was this secret referenced by any ingresses?
                var ingressTls = Namespace(secret.Namespace()).GetIngressesReferencingSecret(key);
                ingressWithoutSecret = ingressTls.Any();
            }

            if (ingressWithoutSecret)
            {
                _logger.LogWarning("Secret was deleted but still referenced by ingresses!");
            }

            _certificateCache.RemoveSecret(key);
        }
#endif
    }

    public bool TryGetReconcileData(NamespacedName key, out ReconcileData data)
    {
        return Namespace(key.Namespace).TryLookup(key, out data);
    }

    public void GetKeys(List<NamespacedName> keys)
    {
        lock (_sync)
        {
            foreach (var (ns, cache) in _namespaceCaches)
            {
                cache.GetKeys(ns, keys);
            }
        }
    }

    public IEnumerable<IngressData> GetIngresses()
    {
        var ingresses = new List<IngressData>();

        lock (_sync)
        {
            foreach (var ns in _namespaceCaches)
            {
                ingresses.AddRange(ns.Value.GetIngresses());
            }
        }

        return ingresses;
    }

    private bool IsYarpIngress(V1IngressSpec spec)
    {
        if (spec.IngressClassName != null)
        {
            lock (_sync)
            {
                return _ingressClassData.ContainsKey(spec.IngressClassName);
            }
        }

        return _isDefaultController;
    }

    private NamespaceCache Namespace(string key)
    {
        lock (_sync)
        {
            if (!_namespaceCaches.TryGetValue(key, out var value))
            {
                value = new NamespaceCache(_certificateCache);
                _namespaceCaches.Add(key, value);
            }
            return value;
        }
    }
}
