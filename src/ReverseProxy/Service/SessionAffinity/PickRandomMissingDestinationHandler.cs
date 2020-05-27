// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Http;
using Microsoft.ReverseProxy.Abstractions.BackendDiscovery.Contract;
using Microsoft.ReverseProxy.RuntimeModel;

namespace Microsoft.ReverseProxy.Service.SessionAffinity
{
    internal class PickRandomMissingDestinationHandler : IMissingDestinationHandler
    {
        private readonly Random _random = new Random();

        public string Name => SessionAffinityBuiltIns.MissingDestinationHandlers.PickRandom;

        public IReadOnlyList<DestinationInfo> Handle(HttpContext context, BackendConfig.BackendSessionAffinityOptions options, object affinityKey, IReadOnlyList<DestinationInfo> availableDestinations)
        {
            if (availableDestinations.Count == 0)
            {
                return availableDestinations;
            }

            var index = _random.Next(availableDestinations.Count);
            return new[] { availableDestinations[index] };
        }
    }
}
