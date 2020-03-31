// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.ReverseProxy.Core.Abstractions;
using Microsoft.ReverseProxy.Core.ConfigModel;

namespace Microsoft.ReverseProxy.Core.Service
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
