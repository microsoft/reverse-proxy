// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;

namespace Microsoft.ReverseProxy.Service
{
    /// <summary>
    /// Provides a method to validate a route.
    /// </summary>
    internal interface IRouteValidator
    {
        /// <summary>
        /// Validates a route, logs any issues, and returns true or false.
        /// </summary>
        Task<bool> ValidateRouteAsync(ProxyRoute route);
    }
}
