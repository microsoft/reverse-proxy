// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

namespace Microsoft.ReverseProxy.RuntimeModel
{
    public sealed class DestinationDynamicState
    {
        public DestinationHealthState Health { get; } = new DestinationHealthState();
    }
}
