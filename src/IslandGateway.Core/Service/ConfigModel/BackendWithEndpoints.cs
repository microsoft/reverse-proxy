// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Core.ConfigModel
{
    internal class BackendWithEndpoints
    {
        public BackendWithEndpoints(Backend backend, IList<BackendEndpoint> endpoints)
        {
            Contracts.CheckValue(backend, nameof(backend));
            Contracts.CheckValue(endpoints, nameof(endpoints));
            Backend = backend;
            Endpoints = endpoints;
        }

        public Backend Backend { get; }
        public IList<BackendEndpoint> Endpoints { get; }
    }
}
