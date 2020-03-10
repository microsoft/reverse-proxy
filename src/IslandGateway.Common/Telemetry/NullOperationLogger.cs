// <copyright file="NullOperationLogger.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Telemetry;

namespace IslandGateway.Common.Telemetry
{
    /// <summary>
    /// Implementation of <see cref="IOperationLogger"/>
    /// which doesn't log anything.
    /// </summary>
    public class NullOperationLogger : IOperationLogger
    {
        /// <inheritdoc/>
        public IOperationContext Context => new NullOperationContext();

        /// <inheritdoc/>
        public void Execute(string operationName, Action action)
        {
            action();
        }

        /// <inheritdoc/>
        public TResult Execute<TResult>(string operationName, Func<TResult> func)
        {
            return func();
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string operationName, Func<Task> func)
        {
            await func();
        }

        /// <inheritdoc/>
        public async Task<TResult> ExecuteAsync<TResult>(string operationName, Func<Task<TResult>> func)
        {
            return await func();
        }
    }
}
