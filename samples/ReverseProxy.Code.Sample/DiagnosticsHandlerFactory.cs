// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

// These classes are a workaround for the lack of distributed tracing support in SocketsHttpHandler before .NET 6.0
#if !NET6_0_OR_GREATER
#nullable enable

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Yarp.ReverseProxy.Forwarder
{
    /// <summary>
    /// A compatibility workaround used to enable distributed tracing support for YARP when running .NET 3.1 or 5.0
    /// </summary>
    internal sealed class DiagnosticsHandlerFactory : ForwarderHttpClientFactory
    {
        protected override HttpMessageHandler WrapHandler(ForwarderHttpClientContext context, HttpMessageHandler handler)
        {
            handler = base.WrapHandler(context, handler);
            return new DiagnosticsHandler(handler);
        }

        // A modified copy of DiagnosticsHandler based on the internal version that ships with .NET 5.0
        // https://github.com/dotnet/runtime/blob/release/5.0/src/libraries/System.Net.Http/src/System/Net/Http/DiagnosticsHandler.cs
        private sealed class DiagnosticsHandler : DelegatingHandler
        {
            private static readonly DiagnosticListener s_diagnosticListener = new("HttpHandlerDiagnosticListener");

            private const string RequestIdHeaderName            = "Request-Id";
            private const string CorrelationContextHeaderName   = "Correlation-Context";
            private const string BaggageHeaderName              = "baggage";
            private const string TraceParentHeaderName          = "traceparent";
            private const string TraceStateHeaderName           = "tracestate";
            private const string HeaderNameToUseForBaggage      = CorrelationContextHeaderName; // Feel free to change this to BaggageHeaderName

            private const string ExceptionEventName = "System.Net.Http.Exception";
            private const string ActivityName       = "System.Net.Http.HttpRequestOut";
            private const string ActivityStartName  = ActivityName + ".Start";
            private const string ActivityStopName   = ActivityName + ".Stop";

            public DiagnosticsHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return ShouldLogDiagnostics(request, out var activity)
                    ? SendWithDiagnosticsAsync(request, activity, cancellationToken)
                    : base.SendAsync(request, cancellationToken);
            }

            private static bool ShouldLogDiagnostics(HttpRequestMessage request, out Activity? activity)
            {
                if (request is null)
                {
                    throw new ArgumentNullException(nameof(request));
                }

                RemoveDistributedContextHeaders(request.Headers);

                var diagnosticListenerEnabled = s_diagnosticListener.IsEnabled();

                if (Activity.Current is not null || (diagnosticListenerEnabled && s_diagnosticListener.IsEnabled(ActivityName, request)))
                {
                    // If a diagnostics listener is enabled for the Activity, always create one
                    activity = new Activity(ActivityName);

                    activity.Start();

                    if (diagnosticListenerEnabled && s_diagnosticListener.IsEnabled(ActivityStartName))
                    {
                        s_diagnosticListener.Write(ActivityStartName, new ActivityStartData(request));
                    }

                    InjectHeaders(activity, request.Headers);

                    return true;
                }
                else
                {
                    activity = null;

                    // There is no Activity, but we may still want to use the instrumented SendWithDiagnosticsAsync if diagnostic listeners are interested in other events
                    return diagnosticListenerEnabled;
                }
            }

            private static void RemoveDistributedContextHeaders(HttpHeaders headers)
            {
                // Match YARP's .NET 6.0+ behavior of removing all of these headers
                // https://github.com/microsoft/reverse-proxy/blob/ff769d6c75cdcf56848a5da29990bf9df541aafe/src/ReverseProxy/Forwarder/RequestUtilities.cs#L86-L93
                headers.Remove(RequestIdHeaderName);
                headers.Remove(CorrelationContextHeaderName);
                headers.Remove(BaggageHeaderName);
                headers.Remove(TraceParentHeaderName);
                headers.Remove(TraceStateHeaderName);
            }

            private static void InjectHeaders(Activity activity, HttpHeaders headers)
            {
                if (activity.IdFormat == ActivityIdFormat.W3C)
                {
                    headers.TryAddWithoutValidation(TraceParentHeaderName, activity.Id);
                    if (activity.TraceStateString is { } traceStateString)
                    {
                        headers.TryAddWithoutValidation(TraceStateHeaderName, traceStateString);
                    }
                }
                else
                {
                    headers.TryAddWithoutValidation(RequestIdHeaderName, activity.Id);
                }

                using var e = activity.Baggage.GetEnumerator();
                if (e.MoveNext())
                {
                    var baggage = new StringBuilder();
                    do
                    {
                        var item = e.Current;
                        baggage.Append(Uri.EscapeDataString(item.Key));
                        baggage.Append('=');
                        baggage.Append(Uri.EscapeDataString(item.Value ?? string.Empty));
                        baggage.Append(", ");
                    }
                    while (e.MoveNext());

                    baggage.Length -= 2; // Account for the last ", "

                    headers.TryAddWithoutValidation(HeaderNameToUseForBaggage, baggage.ToString());
                }
            }

            private async Task<HttpResponseMessage> SendWithDiagnosticsAsync(HttpRequestMessage request, Activity? activity, CancellationToken cancellationToken)
            {
                HttpResponseMessage? response = null;
                var taskStatus = TaskStatus.RanToCompletion;
                try
                {
                    response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                    return response;
                }
                catch (OperationCanceledException)
                {
                    taskStatus = TaskStatus.Canceled;
                    throw;
                }
                catch (Exception ex)
                {
                    if (s_diagnosticListener.IsEnabled(ExceptionEventName))
                    {
                        s_diagnosticListener.Write(ExceptionEventName, new ExceptionData(ex, request));
                    }

                    taskStatus = TaskStatus.Faulted;
                    throw;
                }
                finally
                {
                    if (activity is not null)
                    {
                        activity.SetEndTime(DateTime.UtcNow);

                        if (s_diagnosticListener.IsEnabled(ActivityStopName))
                        {
                            s_diagnosticListener.Write(ActivityStopName, new ActivityStopData(response, request, taskStatus));
                        }

                        activity.Stop();
                    }
                }
            }

            private sealed class ActivityStartData
            {
                public ActivityStartData(HttpRequestMessage request)
                {
                    Request = request;
                }

                public HttpRequestMessage Request { get; }

                public override string ToString() => $"{{ {nameof(Request)} = {Request} }}";
            }

            private sealed class ActivityStopData
            {
                public ActivityStopData(HttpResponseMessage? response, HttpRequestMessage request, TaskStatus requestTaskStatus)
                {
                    Response = response;
                    Request = request;
                    RequestTaskStatus = requestTaskStatus;
                }

                public HttpResponseMessage? Response { get; }
                public HttpRequestMessage Request { get; }
                public TaskStatus RequestTaskStatus { get; }

                public override string ToString() => $"{{ {nameof(Response)} = {Response}, {nameof(Request)} = {Request}, {nameof(RequestTaskStatus)} = {RequestTaskStatus} }}";
            }

            private sealed class ExceptionData
            {
                public ExceptionData(Exception exception, HttpRequestMessage request)
                {
                    Exception = exception;
                    Request = request;
                }

                public Exception Exception { get; }
                public HttpRequestMessage Request { get; }

                public override string ToString() => $"{{ {nameof(Exception)} = {Exception}, {nameof(Request)} = {Request} }}";
            }
        }
    }
}
#endif
