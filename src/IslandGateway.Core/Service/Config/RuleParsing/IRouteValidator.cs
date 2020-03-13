// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Provides a method to validate a route.
    /// </summary>
    internal interface IRouteValidator
    {
        /// <summary>
        /// Validates a route and reports any errors to <paramref name="errorReporter"/>.
        /// </summary>
        bool ValidateRoute(ParsedRoute route, IConfigErrorReporter errorReporter);
    }
}
