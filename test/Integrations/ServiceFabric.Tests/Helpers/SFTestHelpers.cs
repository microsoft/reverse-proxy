// <copyright file="SFTestHelpers.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Fabric.Query;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ServiceFabric.Services.Communication;

namespace Microsoft.ReverseProxy.ServiceFabricIntegration.Tests
{
    /// <summary>
    /// Factory helper class for tests related to Service Fabric integration.
    /// </summary>
    internal static class SFTestHelpers
    {
        // Factory
        internal static ApplicationWrapper FakeApp(string appTypeName, string appTypeVersion = "1.2.3")
        {
            return new ApplicationWrapper
            {
                ApplicationName = new Uri($"fabric:/{appTypeName}"),
                ApplicationTypeName = appTypeName,
                ApplicationTypeVersion = appTypeVersion,
            };
        }
        internal static ServiceWrapper FakeService(Uri serviceName, string serviceTypeName, string serviceManifestVersion = "2.3.4", ServiceKind serviceKind = ServiceKind.Stateless)
        {
            return new ServiceWrapper
            {
                ServiceName = serviceName,
                ServiceTypeName = serviceTypeName,
                ServiceManifestVersion = serviceManifestVersion,
                ServiceKind = serviceKind,
            };
        }
        internal static Guid FakePartition()
        {
            return Guid.NewGuid();
        }
        internal static ReplicaWrapper FakeReplica(Uri serviceName, int id)
        {
            string address = $"https://127.0.0.1/{serviceName.Authority}/{id}";
            return new ReplicaWrapper
            {
                Id = id,
                ReplicaAddress = $"{{'Endpoints': {{'': '{address}' }} }}".Replace("'", "\""),
                HealthState = HealthState.Ok,
                ReplicaStatus = ServiceReplicaStatus.Ready,
            };
        }

        internal static Dictionary<string, string> DummyLabels(string backendId, bool enableGateway = true)
        {
            return new Dictionary<string, string>()
            {
                { "IslandGateway.Enable", enableGateway ? "true" : "false" },
                { "IslandGateway.Backend.BackendId", backendId },
                { "IslandGateway.Backend.CircuitBreaker.MaxConcurrentRequests", "42" },
                { "IslandGateway.Backend.CircuitBreaker.MaxConcurrentRetries", "5" },
                { "IslandGateway.Backend.Quota.Average", "1.2" },
                { "IslandGateway.Backend.Quota.Burst", "2.3" },
                { "IslandGateway.Backend.Partitioning.Count", "5" },
                { "IslandGateway.Backend.Partitioning.KeyExtractor", "Header('x-ms-organization-id')" },
                { "IslandGateway.Backend.Partitioning.Algorithm", "SHA256" },
                { "IslandGateway.Backend.Healthcheck.Interval", "PT5S" },
                { "IslandGateway.Backend.Healthcheck.Timeout", "PT5S" },
                { "IslandGateway.Backend.Healthcheck.Port", "8787" },
                { "IslandGateway.Backend.Healthcheck.Path", "/api/health" },
                { "IslandGateway.Metadata.Foo", "Bar" },
                { "IslandGateway.Routes.MyRoute.Rule", "Host('example.com)" },
                { "IslandGateway.Routes.MyRoute.Priority", "2" },
            };
        }

        /// <summary>
        /// Build an Island Gateway Endpoint from a Service Fabric ReplicaWrapper. Parameter healthListenerName is optional.
        /// If the healthListenerName is set, function would fill in the endpoint url for healthaddress.
        /// </summary>
        /// <remarks>
        /// The address JSON of the replica is expected to have exactly one endpoint, and that one will be used.
        /// </remarks>
        internal static KeyValuePair<string, Destination> BuildDestinationFromReplica(ReplicaWrapper replica, string healthListenerName = null)
        {
            ServiceEndpointCollection.TryParseEndpointsString(replica.ReplicaAddress, out ServiceEndpointCollection endpoints);
            endpoints.TryGetFirstEndpointAddress(out string address);

            string healthAddressUri = null;
            if (healthListenerName != null)
            {
                endpoints.TryGetEndpointAddress(healthListenerName, out healthAddressUri);
            }

            return KeyValuePair.Create(
                replica.Id.ToString(),
                new Destination
                {
                    Address = address,
                    HealthAddress = healthAddressUri,
                    Metadata = null,
                });
        }
    }
}
