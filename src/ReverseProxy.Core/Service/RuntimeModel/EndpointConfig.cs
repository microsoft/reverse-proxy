// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.RuntimeModel
{
    /// <summary>
    /// Immutable representation of the portions of an endpoint
    /// that only change in reaction to configuration changes
    /// (e.g. endpoint address).
    /// </summary>
    /// <remarks>
    /// All members must remain immutable to avoid thread safety issues.
    /// Instead, instances of <see cref="EndpointConfig"/> are replaced
    /// in ther entirety when values need to change.
    /// </remarks>
    public sealed class EndpointConfig
    {
        public EndpointConfig(string address)
        {
            Contracts.CheckNonEmpty(address, nameof(address));
            Address = address;
        }

        // TODO: Make this a Uri.
        public string Address { get; }
    }
}
