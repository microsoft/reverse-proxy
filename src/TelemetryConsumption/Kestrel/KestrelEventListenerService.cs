// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class KestrelEventListenerService : EventListenerService<KestrelEventListenerService, IKestrelTelemetryConsumer, KestrelMetrics>
{
    protected override string EventSourceName => "Microsoft-AspNetCore-Server-Kestrel";

    protected override int NumberOfMetrics => 10;

    public KestrelEventListenerService(ILogger<KestrelEventListenerService> logger, IEnumerable<IKestrelTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<KestrelMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(IKestrelTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

#if NET
        switch (eventData.EventId)
        {
            case 3:
                Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 5);
                {
                    var connectionId = (string)payload[0];
                    var requestId = (string)payload[1];
                    var httpVersion = (string)payload[2];
                    var path = (string)payload[3];
                    var method = (string)payload[4];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStart(eventData.TimeStamp, connectionId, requestId, httpVersion, path, method);
                    }
                }
                break;

            case 4:
                Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == 5);
                {
                    var connectionId = (string)payload[0];
                    var requestId = (string)payload[1];
                    var httpVersion = (string)payload[2];
                    var path = (string)payload[3];
                    var method = (string)payload[4];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStop(eventData.TimeStamp, connectionId, requestId, httpVersion, path, method);
                    }
                }
                break;
        }
#else
        switch (eventData.EventId)
        {
            case 3:
                Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 2);
                {
                    var connectionId = (string)payload[0];
                    var requestId = (string)payload[1];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStart(eventData.TimeStamp, connectionId, requestId);
                    }
                }
                break;

            case 4:
                Debug.Assert(eventData.EventName == "RequestStop" && payload.Count == 2);
                {
                    var connectionId = (string)payload[0];
                    var requestId = (string)payload[1];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStop(eventData.TimeStamp, connectionId, requestId);
                    }
                }
                break;
        }
#endif
    }

    protected override bool TrySaveMetric(KestrelMetrics metrics, string name, double value)
    {
        var longValue = (long)value;

        switch (name)
        {
            case "connections-per-second":
                metrics.ConnectionRate = longValue;
                break;

            case "total-connections":
                metrics.TotalConnections = longValue;
                break;

            case "tls-handshakes-per-second":
                metrics.TlsHandshakeRate = longValue;
                break;

            case "total-tls-handshakes":
                metrics.TotalTlsHandshakes = longValue;
                break;

            case "current-tls-handshakes":
                metrics.CurrentTlsHandshakes = longValue;
                break;

            case "failed-tls-handshakes":
                metrics.FailedTlsHandshakes = longValue;
                break;

            case "current-connections":
                metrics.CurrentConnections = longValue;
                break;

            case "connection-queue-length":
                metrics.ConnectionQueueLength = longValue;
                break;

            case "request-queue-length":
                metrics.RequestQueueLength = longValue;
                break;

            case "current-upgraded-requests":
                metrics.CurrentUpgradedRequests = longValue;
                break;

            default:
                return false;
        }

        return true;
    }
}
