// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.ReverseProxy.Telemetry
{
    /// <summary>
    /// ActivityPropagationHandler propagates the current Activity to the downstream service
    /// </summary>
    internal sealed class ActivityPropagationHandler : DelegatingHandler
    {
        private const string RequestIdHeaderName = "Request-Id";
        private const string CorrelationContextHeaderName = "Correlation-Context";

        private const string TraceParentHeaderName = "traceparent";
        private const string TraceStateHeaderName = "tracestate";

        /// <summary>
        /// ActivityPropagationHandler constructor
        /// </summary>
        /// <param name="innerHandler">Inner handler: Windows or Unix implementation of HttpMessageHandler.
        /// Note that ActivityPropagationHandler is the latest in the pipeline </param>
        public ActivityPropagationHandler(HttpMessageHandler innerHandler) : base(innerHandler)
        {
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            // This handler is conditionally inserted by the ProxyHttpClientFactory based on the configuration
            // If inserted it will insert the necessary headers to propagate the current activity context to
            // the downstream service, if there is a current activity

            if (request == null)
            {
                throw new ArgumentNullException(nameof(request)); //, SR.net_http_handler_norequest); TODO: Is this really necessary?
            }

            // If we are on at all, we propagate current activity information
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                InjectHeaders(currentActivity, request);
            }

            return base.SendAsync(request, cancellationToken);
        }

        #region private

        private void InjectHeaders(Activity currentActivity, HttpRequestMessage request)
        {
            if (currentActivity.IdFormat == ActivityIdFormat.W3C)
            {
                request.Headers.Remove(TraceParentHeaderName);

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
                        baggage.Add(new NameValueHeaderValue(Uri.EscapeDataString(item.Key), Uri.EscapeDataString(item.Value)).ToString());
                    }
                    while (e.MoveNext());
                    request.Headers.TryAddWithoutValidation(CorrelationContextHeaderName, baggage);
                }
            }
        }

        #endregion
    }
}
