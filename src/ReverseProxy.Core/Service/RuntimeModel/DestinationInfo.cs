// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.Util;
using Microsoft.ReverseProxy.Utilities;
using Microsoft.ReverseProxy.Signals;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    /// <summary>
    /// Representation of a backend's destination for use at runtime.
    /// </summary>
    /// <remarks>
    /// Note that while this class is immutable, specific members such as
    /// <see cref="Config"/> and <see cref="DynamicState"/> hold mutable references
    /// that can be updated atomically and which will always have latest information
    /// relevant to this endpoint.
    /// All members are thread safe.
    /// </remarks>
    public sealed class DestinationInfo
    {
        public DestinationInfo(string destinationId)
        {
            Contracts.CheckNonEmpty(destinationId, nameof(destinationId));
            DestinationId = destinationId;
        }

        public string DestinationId { get; }

        /// <summary>
        /// Encapsulates parts of an endpoint that can change atomically
        /// in reaction to config changes.
        /// </summary>
        public Signal<DestinationConfig> Config { get; } = SignalFactory.Default.CreateSignal<DestinationConfig>();

        /// <summary>
        /// Encapsulates parts of an destination that can change atomically
        /// in reaction to runtime state changes (e.g. endpoint health states).
        /// </summary>
        public Signal<DestinationDynamicState> DynamicState { get; } = SignalFactory.Default.CreateSignal<DestinationDynamicState>();

        /// <summary>
        /// Keeps track of the total number of concurrent requests on this endpoint.
        /// </summary>
        public AtomicCounter ConcurrencyCounter { get; } = new AtomicCounter();
    }
}
