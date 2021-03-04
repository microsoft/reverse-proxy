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
            var probeAddress = !string.IsNullOrEmpty(destinationConfig.Options.Health) ? destinationConfig.Options.Health : destinationConfig.Options.Address;
            var probePath = clusterConfig.Options.HealthCheck.Active.Path;
            UriHelper.FromAbsolute(probeAddress, out var destinationScheme, out var destinationHost, out var destinationPathBase, out _, out _);
            var probeUri = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, probePath, default);
            return new HttpRequestMessage(HttpMethod.Get, probeUri)
            {
                Version = clusterConfig.Options.HttpRequest?.Version ?? HttpVersion.Version20,
#if NET
                VersionPolicy = clusterConfig.Options.HttpRequest?.VersionPolicy ?? HttpVersionPolicy.RequestVersionOrLower
#endif
            };
        }
    }
}
