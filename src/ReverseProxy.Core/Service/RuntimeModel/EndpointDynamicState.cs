// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    public sealed class EndpointDynamicState
    {
        public EndpointDynamicState(
            EndpointHealth health)
        {
            Health = health;
        }

        public EndpointHealth Health { get; }
    }
}
