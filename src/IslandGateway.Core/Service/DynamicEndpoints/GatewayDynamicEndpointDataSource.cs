// <copyright file="GatewayDynamicEndpointDataSource.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.Primitives;
using AspNetCore = Microsoft.AspNetCore;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Implementation of <see cref="AspNetCore.Routing.EndpointDataSource"/> that supports being dynamically updated
    /// in a thread-safe manner while avoiding locks on the hot path.
    /// </summary>
    /// <remarks>
    /// This takes inspiration from <a href="https://github.com/aspnet/AspNetCore/blob/master/src/Mvc/Mvc.Core/src/Routing/ActionEndpointDataSourceBase.cs"/>.
    /// </remarks>
    internal class GatewayDynamicEndpointDataSource : AspNetCore.Routing.EndpointDataSource, IGatewayDynamicEndpointDataSource
    {
        private readonly object _syncRoot = new object();

        private List<AspNetCore.Http.Endpoint> _endpoints;
        private CancellationTokenSource _cancellationTokenSource;
        private IChangeToken _changeToken;

        /// <summary>
        /// Initializes a new instance of the <see cref="GatewayDynamicEndpointDataSource"/> class.
        /// </summary>
        public GatewayDynamicEndpointDataSource()
        {
            this.Update(new List<AspNetCore.Http.Endpoint>());
        }

        /// <inheritdoc/>
        public override IReadOnlyList<AspNetCore.Http.Endpoint> Endpoints
        {
            get
            {
                return Volatile.Read(ref this._endpoints);
            }
        }

        /// <inheritdoc/>
        public override IChangeToken GetChangeToken()
        {
            return Volatile.Read(ref this._changeToken);
        }

        /// <summary>
        /// Applies a new set of ASP .NET Core endpoints. Changes take effect immediately.
        /// </summary>
        /// <param name="endpoints">New endpoints to apply.</param>
        public void Update(List<AspNetCore.Http.Endpoint> endpoints)
        {
            if (endpoints == null)
            {
                throw new ArgumentNullException(nameof(endpoints));
            }

            lock (this._syncRoot)
            {
                // These steps are done in a specific order to ensure callers always see a consistent state.

                // Step 1 - capture old token
                var oldCancellationTokenSource = this._cancellationTokenSource;

                // Step 2 - update endpoints
                Volatile.Write(ref this._endpoints, endpoints);

                // Step 3 - create new change token
                this._cancellationTokenSource = new CancellationTokenSource();
                Volatile.Write(ref this._changeToken, new CancellationChangeToken(this._cancellationTokenSource.Token));

                // Step 4 - trigger old token
                oldCancellationTokenSource?.Cancel();
            }
        }
    }
}
