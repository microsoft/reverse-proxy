// <copyright file="BackendEndpoint.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;

namespace IslandGateway.Core.Abstractions
{
    /// <summary>
    /// Describes an endpoint of a backend. Not to be confused with ASP .NET Core Endpoint Routing.
    /// </summary>
    public sealed class BackendEndpoint : IDeepCloneable<BackendEndpoint>
    {
        /// <summary>
        /// Unique identifier of this endpoint. This must be globally unique.
        /// </summary>
        public string EndpointId { get; set; }

        /// <summary>
        /// Address of this endpoint. E.g. <c>https://127.0.0.1:123/abcd1234/</c>.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// Arbitrary key-value pairs that further describe this endpoint.
        /// </summary>
        public IDictionary<string, string> Metadata { get; set; }

        /// <inheritdoc/>
        BackendEndpoint IDeepCloneable<BackendEndpoint>.DeepClone()
        {
            return new BackendEndpoint
            {
                EndpointId = this.EndpointId,
                Address = this.Address,
                Metadata = this.Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}
