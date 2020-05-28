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
        public string Name => SessionAffinityBuiltIns.AffinityFailurePolicies.Return503Error;

        public Task<bool> Handle(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, AffinityStatus affinityStatus)
        {
            context.Response.StatusCode = 503;
            return Task.FromResult(true);
        }
    }
}
