// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Core.Abstractions;

namespace Microsoft.ReverseProxy.Core.Service
{
    internal class InMemoryRoutesRepo : InMemoryListBase<ProxyRoute>, IRoutesRepo
    {
        /// <inheritdoc/>
        public Task<IList<ProxyRoute>> GetRoutesAsync(CancellationToken cancellation)
        {
            return Task.FromResult(Get());
        }

        /// <inheritdoc/>
        public Task SetRoutesAsync(IList<ProxyRoute> routes, CancellationToken cancellation)
        {
            Set(routes);
            return Task.CompletedTask;
        }
    }
}
