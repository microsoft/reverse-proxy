// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.Forwarder
{
    /// <summary>
    /// ActivityPropagationHandler propagates the current Activity to the downstream service
    /// </summary>
    public sealed class ActivityPropagationHandler : DelegatingHandler
    {
        private const string RequestIdHeaderName = "Request-Id";
        private const string CorrelationContextHeaderName = "Correlation-Context";
        private const string BaggageHeaderName = "baggage";

        private const string TraceParentHeaderName = "traceparent";
        private const string TraceStateHeaderName = "tracestate";

        private readonly ActivityContextHeaders _activityContextHeaders;

        public ActivityPropagationHandler(ActivityContextHeaders activityContextHeaders, HttpMessageHandler innerHandler) : base(innerHandler)
        {
            _activityContextHeaders = activityContextHeaders;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // This handler is conditionally inserted by the ProxyHttpClientFactory based on the configuration
            // If inserted it will insert the necessary headers to propagate the current activity context to
            // the downstream service, if there is a current activity

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            // If we are on at all, we propagate current activity information
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                InjectHeaders(currentActivity, request);
            }

            return base.SendAsync(request, cancellationToken);
        }

        private void InjectHeaders(Activity currentActivity, HttpRequestMessage request)
        {
            if (currentActivity.IdFormat == ActivityIdFormat.W3C)
            {
                request.Headers.Remove(TraceParentHeaderName);
                request.Headers.Remove(TraceStateHeaderName);

                request.Headers.TryAddWithoutValidation(TraceParentHeaderName, currentActivity.Id);
                if (currentActivity.TraceStateString != null)
                {
                    request.Headers.TryAddWithoutValidation(TraceStateHeaderName, currentActivity.TraceStateString);
                }
            }
            else
            {
                request.Headers.Remove(RequestIdHeaderName);
                request.Headers.TryAddWithoutValidation(RequestIdHeaderName, currentActivity.Id);
            }

            // we expect baggage to be empty or contain a few items
            using (var e = currentActivity.Baggage.GetEnumerator())
            {
                if (e.MoveNext())
                {
                    var baggage = new List<string>();
                    do
                    {
                        var item = e.Current;
                        baggage.Add(new NameValueHeaderValue(Uri.EscapeDataString(item.Key), Uri.EscapeDataString(item.Value ?? string.Empty)).ToString());
                    }
                    while (e.MoveNext());
                    if (_activityContextHeaders.HasFlag(ActivityContextHeaders.Baggage))
                    {
                        request.Headers.TryAddWithoutValidation(BaggageHeaderName, baggage);
                    }
                    if (_activityContextHeaders.HasFlag(ActivityContextHeaders.CorrelationContext))
                    {
                        request.Headers.TryAddWithoutValidation(CorrelationContextHeaderName, baggage);
                    }
                }
            }
        }
    }
}
