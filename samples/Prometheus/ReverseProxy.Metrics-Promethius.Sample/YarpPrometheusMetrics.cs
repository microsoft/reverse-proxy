using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Middleware;
using Prometheus;

namespace Yarp.Sample
{
    public class YarpPrometheusMetrics
    {
        private static readonly Counter _requestsStarted = Metrics.CreateCounter(
        "yarp_requests_started",
        "Number of requests inititated through the proxy",
        new CounterConfiguration {
            LabelNames =new[] { "Route","Cluster" }
        }
    );



        public Task ReportForYarp(HttpContext context, Func<Task> next)
        {
            var proxyFeature = context.Features.Get<IReverseProxyFeature>();
            _requestsStarted.WithLabels(proxyFeature.RouteSnapshot.ProxyRoute.RouteId, proxyFeature.ClusterSnapshot.Options.Id).Inc();

             return next();

            //???? How do I get more stats after the request?
        }
    }
}
