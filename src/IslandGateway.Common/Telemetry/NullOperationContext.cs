// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Common.Abstractions.Telemetry;

namespace IslandGateway.Common.Telemetry
{
    /// <summary>
    /// Implementation of <see cref="IOperationContext"/>
    /// which doesn't do anything.
    /// </summary>
    public class NullOperationContext : IOperationContext
    {
        /// <inheritdoc/>
        public void SetProperty(string key, string value)
        {
        }
    }
}
