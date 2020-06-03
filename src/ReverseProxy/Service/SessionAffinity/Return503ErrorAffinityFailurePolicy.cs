// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class Return503ErrorAffinityFailurePolicy : IAffinityFailurePolicy
    {
        public string Name => SessionAffinityConstants.AffinityFailurePolicies.Return503Error;

        public Task<bool> Handle(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, AffinityStatus affinityStatus)
        {
            if (affinityStatus == AffinityStatus.OK
                || affinityStatus == AffinityStatus.AffinityKeyNotSet
                || affinityStatus == AffinityStatus.AffinityDisabled)
            {
                // We shouldn't get here, but allow the request to proceed further if that's the case.
                return Task.FromResult(true);
            }

            context.Response.StatusCode = 503;
            return Task.FromResult(false);
        }
    }
}
