// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Fabric.Query;
using Microsoft.ServiceFabric.Services.Communication;
using Yarp.ReverseProxy.Configuration;

namespace Yarp.ReverseProxy.ServiceFabric.Tests
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
        internal static PartitionWrapper FakePartition()
        {
            return new PartitionWrapper
            {
                Id = Guid.NewGuid(),
                Name = "Test"
            };
        }
        internal static ReplicaWrapper FakeReplica(Uri serviceName, int id)
        {
            var address = $"https://127.0.0.1/{serviceName.Authority}/{id}";
            return new ReplicaWrapper
            {
                Id = id,
                ReplicaAddress = $"{{'Endpoints': {{'': '{address}' }} }}".Replace("'", "\""),
                HealthState = HealthState.Ok,
                ReplicaStatus = ServiceReplicaStatus.Ready,
            };
        }

        internal static Dictionary<string, string> DummyLabels(string backendId, bool enableGateway = true, bool activeHealthChecks = false)
        {
            return new Dictionary<string, string>()
            {
                { "YARP.Enable", enableGateway ? "true" : "false" },
                { "YARP.Backend.BackendId", backendId },
                { "YARP.Backend.HealthCheck.Active.Enabled", activeHealthChecks ? "true" : "false" },
                { "YARP.Backend.HealthCheck.Active.Interval", "5" },
                { "YARP.Backend.HealthCheck.Active.Timeout", "5" },
                { "YARP.Backend.HealthCheck.Active.Port", "8787" },
                { "YARP.Backend.HealthCheck.Active.Path", "/api/health" },
                { "YARP.Backend.HealthCheck.Active.Policy", "ConsecutiveFailures" },
                { "YARP.Metadata.Foo", "Bar" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
            };
        }

        /// <summary>
        /// Build a <see cref="DestinationConfig" /> from a Service Fabric <see cref="ReplicaWrapper" />.
        /// </summary>
        /// <remarks>
        /// The address JSON of the replica is expected to have exactly one endpoint, and that one will be used.
        /// </remarks>
        internal static KeyValuePair<string, DestinationConfig> BuildDestinationFromReplicaAndPartition(ReplicaWrapper replica, PartitionWrapper partition, string healthListenerName = null)
        {
            ServiceEndpointCollection.TryParseEndpointsString(replica.ReplicaAddress, out var endpoints);
            endpoints.TryGetFirstEndpointAddress(out var address);

            string healthAddressUri = null;
            if (healthListenerName != null)
            {
                endpoints.TryGetEndpointAddress(healthListenerName, out healthAddressUri);
            }

            var destinationId = $"{partition.Id}/{replica.Id}";

            return KeyValuePair.Create(
                destinationId,
                new DestinationConfig
                {
                    Address = address,
                    Health = healthAddressUri,
                    Metadata = new Dictionary<string, string>
                {
                    { "PartitionId", partition.Id.ToString() ?? string.Empty },
                    { "NamedPartitionName", partition.Name ?? string.Empty },
                    { "ReplicaId", replica.Id.ToString() ?? string.Empty }
                }
                });
        }
    }
}
