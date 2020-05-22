// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class ReturnErrorMissingDestinationHandler : IMissingDestinationHandler
    {
        public string Name => "ReturnError";

        public IReadOnlyList<DestinationInfo> Handle(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, object affinityKey, IReadOnlyList<DestinationInfo> availableDestinations)
        {
            return new DestinationInfo[0];
        }
    }
}
