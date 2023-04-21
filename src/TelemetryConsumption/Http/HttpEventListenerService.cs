// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace Yarp.Telemetry.Consumption;

internal sealed class HttpEventListenerService : EventListenerService<HttpEventListenerService, IHttpTelemetryConsumer, HttpMetrics>
{
    protected override string EventSourceName => "System.Net.Http";

    protected override int NumberOfMetrics => 9;

    public HttpEventListenerService(ILogger<HttpEventListenerService> logger, IEnumerable<IHttpTelemetryConsumer> telemetryConsumers, IEnumerable<IMetricsConsumer<HttpMetrics>> metricsConsumers)
        : base(logger, telemetryConsumers, metricsConsumers)
    { }

    protected override void OnEvent(IHttpTelemetryConsumer[] consumers, EventWrittenEventArgs eventData)
    {
#pragma warning disable IDE0007 // Use implicit type
        // Explicit type here to drop the object? signature of payload elements
        ReadOnlyCollection<object> payload = eventData.Payload!;
#pragma warning restore IDE0007 // Use implicit type

        switch (eventData.EventId)
        {
            case 1:
                Debug.Assert(eventData.EventName == "RequestStart" && payload.Count == 7);
                {
                    var scheme = (string)payload[0];
                    var host = (string)payload[1];
                    var port = (int)payload[2];
                    var pathAndQuery = (string)payload[3];
                    var versionMajor = (int)(byte)payload[4];
                    var versionMinor = (int)(byte)payload[5];
                    var versionPolicy = (HttpVersionPolicy)payload[6];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStart(eventData.TimeStamp, scheme, host, port, pathAndQuery, versionMajor, versionMinor, versionPolicy);
                    }
                }
                break;

            case 2:
                Debug.Assert(eventData.EventName == "RequestStop" /* && payload.Count == 0 */);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestStop(eventData.TimeStamp);
                    }
                }
                break;

            case 3:
                Debug.Assert(eventData.EventName == "RequestFailed" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestFailed(eventData.TimeStamp);
                    }
                }
                break;

            case 4:
                Debug.Assert(eventData.EventName == "ConnectionEstablished" && payload.Count == 2);
                {
                    var versionMajor = (int)(byte)payload[0];
                    var versionMinor = (int)(byte)payload[1];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnConnectionEstablished(eventData.TimeStamp, versionMajor, versionMinor);
                    }
                }
                break;

            case 5:
                Debug.Assert(eventData.EventName == "ConnectionClosed" && payload.Count == 2);
                break;

            case 6:
                Debug.Assert(eventData.EventName == "RequestLeftQueue" && payload.Count == 3);
                {
                    var timeOnQueue = TimeSpan.FromMilliseconds((double)payload[0]);
                    var versionMajor = (int)(byte)payload[1];
                    var versionMinor = (int)(byte)payload[2];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestLeftQueue(eventData.TimeStamp, timeOnQueue, versionMajor, versionMinor);
                    }
                }
                break;

            case 7:
                Debug.Assert(eventData.EventName == "RequestHeadersStart" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestHeadersStart(eventData.TimeStamp);
                    }
                }
                break;

            case 8:
                Debug.Assert(eventData.EventName == "RequestHeadersStop" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestHeadersStop(eventData.TimeStamp);
                    }
                }
                break;

            case 9:
                Debug.Assert(eventData.EventName == "RequestContentStart" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestContentStart(eventData.TimeStamp);
                    }
                }
                break;

            case 10:
                Debug.Assert(eventData.EventName == "RequestContentStop" && payload.Count == 1);
                {
                    var contentLength = (long)payload[0];
                    foreach (var consumer in consumers)
                    {
                        consumer.OnRequestContentStop(eventData.TimeStamp, contentLength);
                    }
                }
                break;

            case 11:
                Debug.Assert(eventData.EventName == "ResponseHeadersStart" && payload.Count == 0);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResponseHeadersStart(eventData.TimeStamp);
                    }
                }
                break;

            case 12:
                Debug.Assert(eventData.EventName == "ResponseHeadersStop" /* && payload.Count == 0 */);
                {
                    foreach (var consumer in consumers)
                    {
                        consumer.OnResponseHeadersStop(eventData.TimeStamp);
                    }
                }
                break;
        }
    }

    protected override bool TrySaveMetric(HttpMetrics metrics, string name, double value)
    {
        switch (name)
        {
            case "requests-started":
                metrics.RequestsStarted = (long)value;
                break;

            case "requests-started-rate":
                metrics.RequestsStartedRate = (long)value;
                break;

            case "requests-failed":
                metrics.RequestsFailed = (long)value;
                break;

            case "requests-failed-rate":
                metrics.RequestsFailedRate = (long)value;
                break;

            case "current-requests":
                metrics.CurrentRequests = (long)value;
                break;

            case "http11-connections-current-total":
                metrics.CurrentHttp11Connections = (long)value;
                break;

            case "http20-connections-current-total":
                metrics.CurrentHttp20Connections = (long)value;
                break;

            case "http11-requests-queue-duration":
                metrics.Http11RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                break;

            case "http20-requests-queue-duration":
                metrics.Http20RequestsQueueDuration = TimeSpan.FromMilliseconds(value);
                break;

            default:
                return false;
        }

        return true;
    }
}
