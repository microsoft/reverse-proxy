// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.Abstractions.ClusterDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class RedistributeAffinityFailurePolicy : IAffinityFailurePolicy
    {
        public string Name => SessionAffinityConstants.AffinityFailurePolicies.Redistribute;

        public Task<bool> Handle(HttpContext context, SessionAffinityOptions options, AffinityStatus affinityStatus)
        {
            if (affinityStatus == AffinityStatus.OK
                || affinityStatus == AffinityStatus.AffinityKeyNotSet)
            {
                throw new InvalidOperationException($"{nameof(RedistributeAffinityFailurePolicy)} is called to handle a successful request's affinity status {affinityStatus}.");
            }

            // Available destinations list have not been changed in the context,
            // so simply allow processing to proceed to load balancing.
            return TaskUtilities.TrueTask;
        }
    }
}
