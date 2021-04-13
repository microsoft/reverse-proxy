using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Middleware;
using Prometheus;
using Yarp.ReverseProxy.Telemetry.Consumption;
using System.Text.Json;

namespace Yarp.Sample
{
    public class PerRequestYarpMetricCollectionMiddleware

    {
        // Required for middleware
        private readonly RequestDelegate _next;


        // Prometheus-net metric registration
        private static readonly string[] _labelNames = new[] { "Route", "Cluster", "Destination" };

        private static readonly CounterConfiguration counterConfig = new CounterConfiguration
        {
            LabelNames = _labelNames
        };

        private static readonly Counter _requestsProcessed = Metrics.CreateCounter(
            "yarp_requests_processed",
            "Number of requests through the proxy",
            counterConfig
        );

        private static readonly Histogram _requestContentBytes = Metrics.CreateHistogram(
            "yarp_request_content_bytes",
            "Bytes for request bodies sent through the proxy",
            new HistogramConfiguration
            {
                LabelNames = _labelNames
            }
         );

        private static readonly Histogram _responseContentBytes = Metrics.CreateHistogram(
            "yarp_response_content_bytes",
            "Bytes for request bodies sent through the proxy",
                    new HistogramConfiguration
                    {
                        LabelNames = _labelNames
                    }
        );

        private static readonly Histogram _requestDuration = Metrics.CreateHistogram(
            "yarp_request_duration",
            "Histogram of request processing durations.",
            new HistogramConfiguration
            {
                LabelNames = _labelNames
            });

        private static readonly Counter _requestsSuccessfull = Metrics.CreateCounter(
            "yarp_requests_success",
            "Number of requests with a 2xx status code",
            counterConfig
        );

        private static readonly Counter _requests_error_4xx = Metrics.CreateCounter(
            "yarp_requests_error_4xx",
            "Number of requests with a 4xx status code",
            counterConfig
        );

        private static readonly Counter _requests_error_5xx = Metrics.CreateCounter(
            "yarp_requests_error_5xx",
            "Number of requests with a 5xx status code",
            counterConfig
        );

        public PerRequestYarpMetricCollectionMiddleware(RequestDelegate next)
        {
            _next = next;
        }


        public async Task InvokeAsync(HttpContext context)
        {
            var startTime = DateTime.UtcNow;

            await _next(context);

            var proxyFeature = context.Features.Get<IReverseProxyFeature>();
            if (proxyFeature != null)
            {
                string[] labelvalues = { proxyFeature.RouteSnapshot.ProxyRoute.RouteId, proxyFeature.ClusterSnapshot.Options.Id, proxyFeature.ProxiedDestination.Config.Options.Address };
                _requestDuration.WithLabels(labelvalues).Observe((startTime - DateTime.UtcNow).TotalMilliseconds);
                _requestsProcessed.WithLabels(labelvalues).Inc();
                if (context.Request.ContentLength.HasValue) { _requestContentBytes.WithLabels(labelvalues).Observe(context.Request.ContentLength.Value); }
                if (context.Response.ContentLength.HasValue) { _responseContentBytes.WithLabels(labelvalues).Observe(context.Response.ContentLength.Value); }

                if (context.Response.StatusCode >= 200 && context.Response.StatusCode < 300) { _requestsSuccessfull.WithLabels(labelvalues).Inc(); }
                else if (context.Response.StatusCode >= 400 && context.Response.StatusCode < 500) { _requests_error_4xx.WithLabels(labelvalues).Inc(); }
                else if (context.Response.StatusCode >= 500 && context.Response.StatusCode < 600) { _requests_error_5xx.WithLabels(labelvalues).Inc(); }
            }
        }
    }
}

