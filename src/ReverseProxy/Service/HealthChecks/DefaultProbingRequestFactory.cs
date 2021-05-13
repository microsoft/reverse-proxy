// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Net;
using System.Net.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Yarp.ReverseProxy.RuntimeModel;

namespace Yarp.ReverseProxy.Service.HealthChecks
{
    internal sealed class DefaultProbingRequestFactory : IProbingRequestFactory
    {
        public HttpRequestMessage CreateRequest(ClusterModel cluster, DestinationModel destination)
        {
            var probeAddress = !string.IsNullOrEmpty(destination.Config.Health) ? destination.Config.Health : destination.Config.Address;
            var probePath = cluster.Config.HealthCheck.Active.Path;
            UriHelper.FromAbsolute(probeAddress, out var destinationScheme, out var destinationHost, out var destinationPathBase, out _, out _);
            var probeUri = UriHelper.BuildAbsolute(destinationScheme, destinationHost, destinationPathBase, probePath, default);
            return new HttpRequestMessage(HttpMethod.Get, probeUri)
            {
                Version = cluster.Config.HttpRequest?.Version ?? HttpVersion.Version20,
#if NET
                VersionPolicy = cluster.Config.HttpRequest?.VersionPolicy ?? HttpVersionPolicy.RequestVersionOrLower
#endif
            };
        }
    }
}
