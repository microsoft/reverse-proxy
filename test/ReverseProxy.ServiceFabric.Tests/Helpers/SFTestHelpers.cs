// <copyright file="SFTestHelpers.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Fabric.Health;
using System.Fabric.Query;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ServiceFabric.Services.Communication;

namespace Microsoft.ReverseProxy.ServiceFabric.Tests
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
                { "YARP.Backend.Healthcheck.Active.Enabled", activeHealthChecks ? "true" : "false" },
                { "YARP.Backend.Healthcheck.Active.Interval", "5" },
                { "YARP.Backend.Healthcheck.Active.Timeout", "5" },
                { "YARP.Backend.Healthcheck.Active.Port", "8787" },
                { "YARP.Backend.Healthcheck.Active.Path", "/api/health" },
                { "YARP.Backend.Healthcheck.Active.Policy", "ConsecutiveFailures" },
                { "YARP.Metadata.Foo", "Bar" },
                { "YARP.Routes.MyRoute.Hosts", "example.com" },
                { "YARP.Routes.MyRoute.Priority", "2" },
            };
        }

        /// <summary>
        /// Build a <see cref="Destination" /> from a Service Fabric <see cref="ReplicaWrapper" />.
        /// </summary>
        /// <remarks>
        /// The address JSON of the replica is expected to have exactly one endpoint, and that one will be used.
        /// </remarks>
        internal static KeyValuePair<string, Destination> BuildDestinationFromReplica(ReplicaWrapper replica, string healthListenerName = null)
        {
            ServiceEndpointCollection.TryParseEndpointsString(replica.ReplicaAddress, out var endpoints);
            endpoints.TryGetFirstEndpointAddress(out var address);

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
                    Health = healthAddressUri,
                    Metadata = null,
                });
        }
    }
}
