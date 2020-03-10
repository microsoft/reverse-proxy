// <copyright file="EndpointDynamicState.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

namespace IslandGateway.Core.RuntimeModel
{
    internal sealed class EndpointDynamicState
    {
        public EndpointDynamicState(
            EndpointHealth health)
        {
            this.Health = health;
        }

        public EndpointHealth Health { get; }
    }
}
