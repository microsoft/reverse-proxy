// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of System.Net.NameResolution EventSource events.
    /// </summary>
    public interface INameResolutionTelemetryConsumer
    {
        /// <summary>
        /// Called before a name resolution.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        /// <param name="hostNameOrAddress">Host name or address we are resolving.</param>
        void OnResolutionStart(DateTime timestamp, string hostNameOrAddress);

        /// <summary>
        /// Called after a name resolution.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnResolutionStop(DateTime timestamp);

        /// <summary>
        /// Called before <see cref="OnResolutionStop(DateTime)"/> if the name resolution failed.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnResolutionFailed(DateTime timestamp);
    }
}
