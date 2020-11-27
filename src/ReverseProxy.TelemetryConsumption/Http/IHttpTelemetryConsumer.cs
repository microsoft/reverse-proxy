// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Net.Http;

namespace Microsoft.ReverseProxy.Telemetry.Consumption
{
    public interface IHttpTelemetryConsumer
    {
        void OnRequestStart(DateTime timestamp, string scheme, string host, int port, string pathAndQuery, int versionMajor, int versionMinor, HttpVersionPolicy versionPolicy);

        void OnRequestStop(DateTime timestamp);

        void OnRequestFailed(DateTime timestamp);

        void OnConnectionEstablished(DateTime timestamp, int versionMajor, int versionMinor);

        void OnRequestLeftQueue(DateTime timestamp, TimeSpan timeOnQueue, int versionMajor, int versionMinor);

        void OnRequestHeadersStart(DateTime timestamp);

        void OnRequestHeadersStop(DateTime timestamp);

        void OnRequestContentStart(DateTime timestamp);

        void OnRequestContentStop(DateTime timestamp, long contentLength);

        void OnResponseHeadersStart(DateTime timestamp);

        void OnResponseHeadersStop(DateTime timestamp);
    }
}
