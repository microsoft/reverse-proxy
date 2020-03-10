// <copyright file="EndpointConfig.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.CoreServicesBorrowed;

namespace IslandGateway.Core.RuntimeModel
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
    internal sealed class EndpointConfig
    {
        public EndpointConfig(string address)
        {
            Contracts.CheckNonEmpty(address, nameof(address));
            this.Address = address;
        }

        // TODO: Make this a Uri.
        public string Address { get; }
    }
}
