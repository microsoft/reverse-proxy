// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions.Telemetry;

namespace Microsoft.ReverseProxy.Telemetry
{
    /// <summary>
    /// Implementation of <see cref="IOperationLogger{TCategoryName}"/>
    /// which doesn't log anything.
    /// </summary>
    public class NullOperationLogger<TCategoryName> : IOperationLogger<TCategoryName>
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
