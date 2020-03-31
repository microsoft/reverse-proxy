// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Linq;
using Microsoft.ReverseProxy.Core.Service.Management;
using Microsoft.ReverseProxy.Core.Service.Proxy.Infra;
using Microsoft.ReverseProxy.Core.Util;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.Signals;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    /// <summary>
    /// Representation of a backend for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> and <see cref="DynamicState"/> hold mutable references
    /// that can be updated atomically and which will always have latest information
    /// relevant to this backend.
    /// All members are thread safe.
    /// </remarks>
    internal sealed class BackendInfo
    {
        public BackendInfo(string backendId, IEndpointManager endpointManager, IProxyHttpClientFactory proxyHttpClientFactory)
        {
            Contracts.CheckNonEmpty(backendId, nameof(backendId));
            Contracts.CheckValue(endpointManager, nameof(endpointManager));
            Contracts.CheckValue(proxyHttpClientFactory, nameof(proxyHttpClientFactory));

            BackendId = backendId;
            EndpointManager = endpointManager;
            ProxyHttpClientFactory = proxyHttpClientFactory;

            DynamicState = CreateDynamicStateQuery();
        }

        public string BackendId { get; }

        public IEndpointManager EndpointManager { get; }

        /// <summary>
        /// Used to create instances of <see cref="System.Net.Http.HttpClient"/>
        /// when proxying requests to this backend.
        /// </summary>
        public IProxyHttpClientFactory ProxyHttpClientFactory { get; }

        /// <summary>
        /// Encapsulates parts of a backend that can change atomically
        /// in reaction to config changes.
        /// </summary>
        public Signal<BackendConfig> Config { get; } = SignalFactory.Default.CreateSignal<BackendConfig>();

        /// <summary>
        /// Encapsulates parts of a backend that can change atomically
        /// in reaction to runtime state changes (e.g. dynamic endpoint discovery).
        /// </summary>
        public IReadableSignal<BackendDynamicState> DynamicState { get; }

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this backend.
        /// </summary>
        public AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();

        /// <summary>
        /// Sets up the data flow that keeps <see cref="DynamicState"/> up to date.
        /// See <c>Util\Signals\Signals.md</c> for more information.
        /// </summary>
        private IReadableSignal<BackendDynamicState> CreateDynamicStateQuery()
        {
            var endpointsAndStateChanges =
                EndpointManager.Items
                    .SelectMany(endpoints =>
                        endpoints
                            .Select(endpoint => endpoint.DynamicState)
                            .AnyChange())
                    .DropValue();

            return new[] { endpointsAndStateChanges, Config.DropValue() }
                .AnyChange() // If any of them change...
                .Select(
                    _ =>
                    {
                        var allEndpoints = EndpointManager.Items.Value ?? new List<EndpointInfo>().AsReadOnly();
                        var healthyEndpoints = (Config.Value?.HealthCheckOptions.Enabled ?? false)
                            ? allEndpoints.Where(endpoint => endpoint.DynamicState.Value?.Health == EndpointHealth.Healthy).ToList().AsReadOnly()
                            : allEndpoints;
                        return new BackendDynamicState(
                            allEndpoints: allEndpoints,
                            healthyEndpoints: healthyEndpoints);
                    });
        }
    }
}
