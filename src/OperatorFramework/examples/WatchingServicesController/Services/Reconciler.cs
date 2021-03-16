// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using k8s.Models;
using Microsoft.Kubernetes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using IngressController.Dispatching;
using WatchingServicesProtocol;

namespace IngressController.Services
{
    /// <summary>
    /// IReconciler is a service interface called by the <see cref="Controller"/> to process
    /// the work items as they are dequeued.
    /// </summary>
    public class Reconciler : IReconciler
    {
        private readonly IDispatcher _dispatcher;
        private Action<IDispatchTarget> _attached;

        public Reconciler(IDispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _dispatcher.OnAttach(Attached);
        }

        public void OnAttach(Action<IDispatchTarget> attached)
        {
            _attached = attached;
        }


        private void Attached(IDispatchTarget target)
        {
            _attached?.Invoke(target);
        }

        public async Task ProcessAsync(IDispatchTarget target, NamespacedName key, ReconcileData data, CancellationToken cancellationToken)
        {
            var message = new Message
            {
                MessageType = MessageType.Update,
                Key = $"{key.Namespace}:{key.Name}",
                Rules = new List<Rule>(),
            };

            var spec = data.Ingress.Spec;
            var defaultBackend = spec?.DefaultBackend;
            var defaultService = defaultBackend?.Service;
            IList<V1EndpointSubset> defaultSubsets = default;
            if (!string.IsNullOrEmpty(defaultService?.Name))
            {
                defaultSubsets = data.EndpointsList.SingleOrDefault(x => x.Name == defaultService?.Name).Subsets;
            }

            foreach (var rule in spec.Rules ?? Enumerable.Empty<V1IngressRule>())
            {
                var http = rule.Http;
                foreach (var path in http.Paths ?? Enumerable.Empty<V1HTTPIngressPath>())
                {
                    var backend = path.Backend;
                    var service = backend.Service;
                    var subsets = defaultSubsets;
                    if (!string.IsNullOrEmpty(service?.Name))
                    {
                        subsets = data.EndpointsList.SingleOrDefault(x => x.Name == service?.Name).Subsets;
                    }

                    foreach (var subset in subsets ?? Enumerable.Empty<V1EndpointSubset>())
                    {
                        foreach (var port in subset.Ports ?? Enumerable.Empty<V1EndpointPort>())
                        {
                            if (Matches(port, service?.Port))
                            {
                                message.Rules.Add(new Rule
                                {
                                    Host = rule.Host,
                                    Path = path.Path,
                                    Port = port.Port,
                                    Ready = subset.Addresses?.Select(addr => addr.Ip)?.ToList(),
                                    NotReady = subset.NotReadyAddresses?.Select(addr => addr.Ip)?.ToList(),
                                }); ;
                            }
                        }
                    }
                }
            }

            var bytes = JsonSerializer.SerializeToUtf8Bytes(message);

            await _dispatcher.SendAsync(target, bytes, cancellationToken);
        }

        private bool Matches(V1EndpointPort port1, V1ServiceBackendPort port2)
        {
            if (port1 == null || port2 == null)
            {
                return false;
            }
            if (port2.Number != null && port2.Number == port1.Port)
            {
                return true;
            }
            if (port2.Name != null && string.Equals(port2.Name, port1.Name, StringComparison.Ordinal))
            {
                return true;
            }
            return false;
        }
    }
}
