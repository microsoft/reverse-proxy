// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;

namespace IslandGateway.Common.Abstractions.Telemetry
{
    /// <summary>
    /// Provides methods to log telemetry for the execution of chunks of
    /// synchronous or asynchronous operations.
    /// </summary>
    public interface IOperationLogger
    {
        /// <summary>
        /// Gets the context for the current operation.
        /// </summary>
        IOperationContext Context { get; }

        /// <summary>
        /// Tracks the execution of the provided <paramref name="action"/>
        /// as an operation named <paramref name="operationName"/>.
        /// </summary>
        /// <param name="operationName">Name of the operation.</param>
        /// <param name="action">Action to execute.</param>
        void Execute(string operationName, Action action);

        /// <summary>
        /// Tracks the execution of the provided <paramref name="func"/>
        /// as an operation named <paramref name="operationName"/>.
        /// </summary>
        /// <param name="operationName">Name of the operation.</param>
        /// <param name="func">Action to execute.</param>
        TResult Execute<TResult>(string operationName, Func<TResult> func);

        /// <summary>
        /// Tracks the asynchronous execution of the provided <paramref name="func"/>
        /// as an operation named <paramref name="operationName"/>.
        /// </summary>
        /// <param name="operationName">Name of the operation.</param>
        /// <param name="func">Action to execute.</param>
        Task ExecuteAsync(string operationName, Func<Task> func);

        /// <summary>
        /// Tracks the asynchronous execution of the provided <paramref name="func"/>
        /// as an operation named <paramref name="operationName"/>.
        /// </summary>
        /// <param name="operationName">Name of the operation.</param>
        /// <param name="func">Action to execute.</param>
        Task<TResult> ExecuteAsync<TResult>(string operationName, Func<Task<TResult>> func);
    }
}
