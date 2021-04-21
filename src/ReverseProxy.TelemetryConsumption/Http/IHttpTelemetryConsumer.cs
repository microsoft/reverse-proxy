// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Yarp.ReverseProxy.Telemetry.Consumption
{
    /// <summary>
    /// A consumer of System.Net.Http EventSource events.
    /// </summary>
    public interface IHttpTelemetryConsumer
    {
        /// <summary>
        /// Called before an HTTP request.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        /// <param name="scheme">Scheme of the request Uri.</param>
        /// <param name="host">Host of the request Uri.</param>
        /// <param name="port">Port of the request Uri.</param>
        /// <param name="pathAndQuery">Path and query of the request Uri.</param>
        /// <param name="versionMajor">Major component of the request's HTTP version.</param>
        /// <param name="versionMinor">Minor component of the request's HTTP version.</param>
        /// <param name="versionPolicy"><see cref="HttpVersionPolicy"/> of the request.</param>
        void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy) { }

        /// <summary>
        /// Called after an HTTP request.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnRequestStop(DateTime timestamp) { }

        /// <summary>
        /// Called before <see cref="OnRequestStop(DateTime)"/> if the request failed.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnRequestFailed(DateTime timestamp) { }

        /// <summary>
        /// Called when a new HTTP connection is established.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        /// <param name="versionMajor">Major component of the connection's HTTP version.</param>
        /// <param name="versionMinor">Minor component of the connection's HTTP version.</param>
        void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor) { }

        /// <summary>
        /// Called when a request that hit the MaxConnectionsPerServer or MAX_CONCURRENT_STREAMS limit leaves the queue.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        /// <param name="timeOnQueue">Time spent on queue.</param>
        /// <param name="versionMajor">Major component of the request's HTTP version.</param>
        /// <param name="versionMinor">Minor component of the request's HTTP version.</param>
        void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor) { }

        /// <summary>
        /// Called before sending the request headers.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnRequestHeadersStart(DateTime timestamp) { }

        /// <summary>
        /// Called after sending the request headers.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnRequestHeadersStop(DateTime timestamp) { }

        /// <summary>
        /// Called before sending the request content.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnRequestContentStart(DateTime timestamp) { }

        /// <summary>
        /// Called after sending the request content.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        /// <param name="contentLength"></param>
        void OnRequestContentStop(DateTime timestamp, long contentLength) { }

        /// <summary>
        /// Called before reading the response headers.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnResponseHeadersStart(DateTime timestamp) { }

        /// <summary>
        /// Called after reading all response headers.
        /// </summary>
        /// <param name="timestamp">Timestamp when the event was fired.</param>
        void OnResponseHeadersStop(DateTime timestamp) { }
    }
}
