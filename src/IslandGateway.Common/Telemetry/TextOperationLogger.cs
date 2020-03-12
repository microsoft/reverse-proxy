// <copyright file="TextOperationLogger.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Threading.Tasks;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Common.Util;
using Microsoft.Extensions.Logging;

namespace IslandGateway.Common.Telemetry
{
    /// <summary>
    /// Default implementation of <see cref="IOperationLogger"/>
    /// which logs activity start / end events as Information messages.
    /// </summary>
    public class TextOperationLogger : IOperationLogger
    {
        private readonly ILogger<TextOperationLogger> logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextOperationLogger"/> class.
        /// </summary>
        /// <param name="logger">Logger where text messages will be logger.</param>
        public TextOperationLogger(ILogger<TextOperationLogger> logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc/>
        // TODO: Implement this.
        public IOperationContext Context => null;

        /// <inheritdoc/>
        public void Execute(string operationName, Action action)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                this.logger.LogInformation($"Operation started: {operationName}");
                action();
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, success");
            }
            catch (Exception ex)
            {
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, error: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public TResult Execute<TResult>(string operationName, Func<TResult> func)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                this.logger.LogInformation($"Operation started: {operationName}");
                var res = func();
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, success");
                return res;
            }
            catch (Exception ex)
            {
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, error: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string operationName, Func<Task> action)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                this.logger.LogInformation($"Operation started: {operationName}");
                await action();
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, success");
            }
            catch (Exception ex)
            {
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, error: {ex.Message}");
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TResult> ExecuteAsync<TResult>(string operationName, Func<Task<TResult>> func)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                this.logger.LogInformation($"Operation started: {operationName}");
                var res = await func();
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, success");
                return res;
            }
            catch (Exception ex)
            {
                this.logger.LogInformation($"Operation ended: {operationName}, {stopwatch.Elapsed.TotalMilliseconds:F1}ms, error: {ex.Message}");
                throw;
            }
        }
    }
}
