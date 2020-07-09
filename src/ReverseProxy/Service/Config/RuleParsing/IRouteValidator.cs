// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Microsoft.ReverseProxy.ConfigModel;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Provides a method to validate a route.
    /// </summary>
    internal interface IRouteValidator
    {
        /// <summary>
        /// Validates a route and reports any errors to <paramref name="errorReporter"/>.
        /// </summary>
        Task<bool> ValidateRouteAsync(ParsedRoute route);
    }
}
