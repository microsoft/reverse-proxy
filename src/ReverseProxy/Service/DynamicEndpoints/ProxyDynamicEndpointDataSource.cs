// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Implementation of <see cref="AspNetCore.Routing.EndpointDataSource"/> that supports being dynamically updated
    /// in a thread-safe manner while avoiding locks on the hot path.
    /// </summary>
    /// <remarks>
    /// This takes inspiration from <a href="https://github.com/aspnet/AspNetCore/blob/master/src/Mvc/Mvc.Core/src/Routing/ActionEndpointDataSourceBase.cs"/>.
    /// </remarks>
    internal class ProxyDynamicEndpointDataSource : AspNetCore.Routing.EndpointDataSource, IProxyDynamicEndpointDataSource
    {
        private readonly object _syncRoot = new object();

        private List<Endpoint> _endpoints;
        private CancellationTokenSource _cancellationTokenSource;
        private IChangeToken _changeToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="ProxyDynamicEndpointDataSource"/> class.
        /// </summary>
        public ProxyDynamicEndpointDataSource()
        {
            Update(new List<Endpoint>());
        }

        /// <inheritdoc/>
        public override IReadOnlyList<Endpoint> Endpoints
        {
            get
            {
                return Volatile.Read(ref _endpoints);
            }
        }

        /// <inheritdoc/>
        public override IChangeToken GetChangeToken()
        {
            return Volatile.Read(ref _changeToken);
        }

        /// <summary>
        /// Applies a new set of ASP .NET Core endpoints. Changes take effect immediately.
        /// </summary>
        /// <param name="endpoints">New endpoints to apply.</param>
        public void Update(List<Endpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            lock (_syncRoot)
            {
                // These steps are done in a specific order to ensure callers always see a consistent state.

                // Step 1 - capture old token
                var oldCancellationTokenSource = _cancellationTokenSource;

                // Step 2 - update endpoints
                Volatile.Write(ref _endpoints, endpoints);

                // Step 3 - create new change token
                _cancellationTokenSource = new CancellationTokenSource();
                Volatile.Write(ref _changeToken, new CancellationChangeToken(_cancellationTokenSource.Token));

                // Step 4 - trigger old token
                oldCancellationTokenSource?.Cancel();
            }
        }
    }
}
