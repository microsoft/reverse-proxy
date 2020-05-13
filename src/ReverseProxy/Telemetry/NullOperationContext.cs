// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Abstractions.Telemetry;

namespace Microsoft.ReverseProxy.Telemetry
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
