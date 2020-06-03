// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class RedistributeAffinityFailurePolicy : IAffinityFailurePolicy
    {
        public string Name => SessionAffinityConstants.AffinityFailurePolicies.Redistribute;

        public Task<bool> Handle(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, AffinityStatus affinityStatus)
        {
            // Available destinations list have not been changed in the context,
            // so simply allow processing to proceed to load balancing.
            return Task.FromResult(true);
        }
    }
}
