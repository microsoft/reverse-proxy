// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.HealthChecks
{
    internal class DefaultProbingRequestFactory : IProbingRequestFactory
    {
        public HttpRequestMessage CreateRequest(ClusterConfig clusterConfig, DestinationConfig destinationConfig)
        {
            var probeAddress = !string.IsNullOrEmpty(destinationConfig.Health) ? destinationConfig.Health : destinationConfig.Address;
            var probePath = clusterConfig.HealthCheckOptions.Active.Path;
            UriHelper.FromAbsolute(probeAddress, out var destinationScheme, out var destinationHost, out var destinationPathBase, out _, out _);
            var probeUri = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, probePath, default);
            var version = HttpVersion.Version20;
            if (clusterConfig.HttpRequestOptions.Version != default)
            {
                version = clusterConfig.HttpRequestOptions.Version;
            }
            return new HttpRequestMessage(HttpMethod.Get, probeUri)
            {
                Version = version,
#if NET
                VersionPolicy = clusterConfig.HttpRequestOptions.VersionPolicy
#endif
            };
        }
    }
}
