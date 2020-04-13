// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.Util;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.Signals;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    /// <summary>
    /// Representation of a backend's endpoint for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> and <see cref="DynamicState"/> hold mutable references
    /// that can be updated atomically and which will always have latest information
    /// relevant to this endpoint.
    /// All members are thread safe.
    /// </remarks>
    public sealed class EndpointInfo
    {
        public EndpointInfo(string endpointId)
        {
            Contracts.CheckNonEmpty(endpointId, nameof(endpointId));
            EndpointId = endpointId;
        }

        public string EndpointId { get; }

        /// <summary>
        /// Encapsulates parts of an endpoint that can change atomically
        /// in reaction to config changes.
        /// </summary>
        public Signal<EndpointConfig> Config { get; } = SignalFactory.Default.CreateSignal<EndpointConfig>();

        /// <summary>
        /// Encapsulates parts of an endpoint that can change atomically
        /// in reaction to runtime state changes (e.g. endpoint health states).
        /// </summary>
        public Signal<EndpointDynamicState> DynamicState { get; } = SignalFactory.Default.CreateSignal<EndpointDynamicState>();

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this endpoint.
        /// </summary>
        public AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();
    }
}
