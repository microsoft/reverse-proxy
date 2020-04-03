// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;

namespace Microsoft.ReverseProxy.Core.Abstractions
{
    /// <summary>
    /// Describes an endpoint of a backend. Not to be confused with ASP .NET Core Endpoint Routing.
    /// </summary>
    public sealed class BackendEndpoint : IDeepCloneable<BackendEndpoint>
    {
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
                Address = Address,
                Metadata = Metadata?.DeepClone(StringComparer.Ordinal),
            };
        }
    }
}
