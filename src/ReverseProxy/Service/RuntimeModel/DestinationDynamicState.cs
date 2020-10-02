// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public sealed class DestinationDynamicState
    {
        public DestinationDynamicState(CompositeDestinationHealth health)
        {
            Health = health;
        }

        public CompositeDestinationHealth Health { get; }
    }
}
