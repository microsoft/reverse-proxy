// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class DefaultProbingRequestFactory : IProbingRequestFactory
    {
        public HttpRequestMessage CreateRequest(ClusterConfig clusterConfig, DestinationInfo destination)
        {
            var probeAddress = !string.IsNullOrEmpty(destination.Config.Health) ? destination.Config.Health : destination.Config.Address;
            var probePath = clusterConfig.HealthCheckOptions.Active.Path;
            UriHelper.FromAbsolute(probeAddress, out var destinationScheme, out var destinationHost, out var destinationPathBase, out _, out _);
            var probeUri = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, probePath, default);
            return new HttpRequestMessage(HttpMethod.Get, probeUri) { Version = ProtocolHelper.Http2Version };
        }
    }
}
