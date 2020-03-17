// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;
using IslandGateway.Core.Abstractions;
using IslandGateway.Core.ConfigModel;

namespace IslandGateway.Core.Service
{
    /// <summary>
    /// Provides a method that uses configuration repositories to build a <see cref="DynamicConfigRoot"/> object.
    /// </summary>
    internal interface IDynamicConfigBuilder
    {
        /// <summary>
        /// Creates a <see cref="DynamicConfigRoot"/> object representing the current desired gateway dynamic configurations.
        /// </summary>
        Task<Result<DynamicConfigRoot>> BuildConfigAsync(IConfigErrorReporter errorReporter, CancellationToken cancellation);
    }
}
