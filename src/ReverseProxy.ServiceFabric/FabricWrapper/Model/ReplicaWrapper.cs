// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Fabric;
using System.Fabric.Health;
using System.Fabric.Query;

namespace Yarp.ReverseProxy.ServiceFabric
{
    /// <summary>
    /// TODO .
    /// </summary>
    internal class ReplicaWrapper
    {
        public long Id { get; set; }
        public string ReplicaAddress { get; set; }
        public ReplicaRole? Role { get; set; }
        public HealthState HealthState { get; set; }
        public ServiceReplicaStatus ReplicaStatus { get; set; }
        public ServiceKind ServiceKind { get; set; }

        /* NOTE: These properties are present in the actual Replica class but excluded from the wrapper. Include if needed.
        public string NodeName { get; }
        public TimeSpan LastInBuildDuration { get; }
        protected internal long LastInBuildDurationInSeconds { get; }
        */
    }
}
