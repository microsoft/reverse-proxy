// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Threading;
using System.Threading.Tasks;

namespace SampleClient.Scenarios
{
    /// <summary>
    ///     Interface for the implementation of a scenario that can be executed asynchronously.
    /// </summary>
    internal interface IScenario
    {
        /// <summary>
        ///     Executes the scenario asynchronously.
        /// </summary>
        Task ExecuteAsync(CommandLineArgs args, CancellationToken cancellation);
    }
}
