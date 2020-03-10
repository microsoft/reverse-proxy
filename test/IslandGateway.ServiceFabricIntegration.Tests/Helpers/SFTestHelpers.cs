// <copyright file="SFTestHelpers.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
using System;
using System.Collections.Generic;

using IslandGateway.Common.Util;
using IslandGateway.Core.Abstractions;
using Microsoft.ServiceFabric.Services.Communication;

namespace IslandGateway.ServiceFabricIntegration.Tests
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
        internal static ServiceWrapper FakeService(Uri serviceName, string serviceTypeName, string serviceManifestVersion = "2.3.4")
        {
            return new ServiceWrapper
            {
                ServiceName = serviceName,
                ServiceTypeName = serviceTypeName,
                ServiceManifestVersion = serviceManifestVersion,
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
            };
        }

        /// <summary>
        /// Build an Island Gateway Endpoint from a Service Fabric ReplicaWrapper.
        /// </summary>
        /// <remarks>
        /// The address JSON of the replica is expected to have exactly one endpoint, and that one will be used.
        /// </remarks>
        internal static BackendEndpoint BuildEndpointFromReplica(ReplicaWrapper replica)
        {
            ServiceEndpointCollection.TryParseEndpointsString(replica.ReplicaAddress, out ServiceEndpointCollection endpoints);
            endpoints.TryGetFirstEndpointAddress(out string address);
            return new BackendEndpoint
            {
                EndpointId = replica.Id.ToString(),
                Address = address,
                Metadata = null,
            };
        }
    }
}