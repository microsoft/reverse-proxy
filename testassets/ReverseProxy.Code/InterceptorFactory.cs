// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Yarp.ReverseProxy.Forwarder;
using Yarp.ReverseProxy.Model;

namespace Yarp.ReverseProxy.Sample
{
    internal class InterceptorFactory : IForwarderHttpClientFactory
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public InterceptorFactory(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context)
        {
            return context.OldClient ?? new HttpMessageInvoker(new Interceptor(_httpContextAccessor));
        }
    }

    internal class Interceptor : HttpMessageHandler
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public Interceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var proxyFeature = httpContext.Features.Get<IReverseProxyFeature>();

            var data = new {
                RouteId = proxyFeature.Route.Config.RouteId,
                RouteMatch = proxyFeature.Route.Config.Match,
                ClusterId = proxyFeature.Cluster.Config.ClusterId,
                DestinationId = proxyFeature.ProxiedDestination.DestinationId,
                DestinationPrefix = proxyFeature.ProxiedDestination.Model.Config.Address,
                Version = request.Version.ToString(2),
#if NET5_0_OR_GREATER
                VersionPolicy = request.VersionPolicy.ToString(),
#endif
                Method = request.Method.Method,
                Uri = request.RequestUri.AbsoluteUri,
                RequestHeaders = request.Headers,
                ContentHeaders = request.Content?.Headers,
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions()
            {
                WriteIndented = true,
            });
            var response = new HttpResponseMessage()
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
                RequestMessage = request,
                Version = request.Version,
            };
            response.Headers.CacheControl = new CacheControlHeaderValue() { NoCache = true };

            return Task.FromResult(response);
        }
    }
}
