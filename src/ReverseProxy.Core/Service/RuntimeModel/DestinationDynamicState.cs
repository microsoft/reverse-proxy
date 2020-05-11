// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    public sealed class DestinationDynamicState
    {
        public DestinationDynamicState(
            DestinationHealth health)
        {
            Health = health;
        }

        public DestinationHealth Health { get; }
    }
}
