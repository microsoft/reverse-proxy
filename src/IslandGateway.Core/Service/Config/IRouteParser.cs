// <copyright file="IRouteParser.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Provides a method to parse a route into a format convenient for internal use in Island Gateway.
    /// </summary>
    internal interface IRouteParser
    {
        /// <summary>
        /// Parses a route into a format convenient for internal use in Island Gateway.
        /// </summary>
        Result<ParsedRoute> ParseRoute(GatewayRoute route, IConfigErrorReporter errorReporter);
    }
}