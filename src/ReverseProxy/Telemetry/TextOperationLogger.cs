// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.ReverseProxy.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Utilities;

namespace Microsoft.ReverseProxy.Telemetry
{
    /// <summary>
    /// Default implementation of <see cref="IOperationLogger"/>
    /// which logs activity start / end events as Information messages.
    /// </summary>
    public class TextOperationLogger<TCategoryName> : IOperationLogger<TCategoryName>
    {
        private readonly ILogger<TCategoryName> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="TextOperationLogger"/> class.
        /// </summary>
        /// <param name="logger">Logger where text messages will be logger.</param>
        public TextOperationLogger(ILogger<TCategoryName> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                Log.OperationStarted(_logger, operationName);
                action();
                Log.OperationEnded(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.OperationFailed(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds, ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public TResult Execute<TResult>(string operationName, Func<TResult> func)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                Log.OperationStarted(_logger, operationName);
                var res = func();
                Log.OperationEnded(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds);
                return res;
            }
            catch (Exception ex)
            {
                Log.OperationFailed(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds, ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task ExecuteAsync(string operationName, Func<Task> action)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                Log.OperationStarted(_logger, operationName);
                await action();
                Log.OperationEnded(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                Log.OperationFailed(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds, ex.Message);
                throw;
            }
        }

        /// <inheritdoc/>
        public async Task<TResult> ExecuteAsync<TResult>(string operationName, Func<Task<TResult>> func)
        {
            var stopwatch = ValueStopwatch.StartNew();
            try
            {
                Log.OperationStarted(_logger, operationName);
                var res = await func();
                Log.OperationEnded(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds);
                return res;
            }
            catch (Exception ex)
            {
                Log.OperationFailed(_logger, operationName, stopwatch.Elapsed.TotalMilliseconds, ex.Message);
                throw;
            }
        }

        private static class Log
        {
            private static readonly Action<ILogger, string, Exception> _operationStarted = LoggerMessage.Define<string>(
                LogLevel.Information,
                EventIds.OperationStarted,
                "Operation started: {operationName}");

            private static readonly Action<ILogger, string, double, Exception> _operationEnded = LoggerMessage.Define<string, double>(
                LogLevel.Information,
                EventIds.OperationEnded,
                "Operation ended: {operationName}, {operationDuration}ms, success");

            private static readonly Action<ILogger, string, double, string, Exception> _operationFailed = LoggerMessage.Define<string, double, string>(
                LogLevel.Information,
                EventIds.OperationFailed,
                "Operation ended: {operationName}, {operationDuration}ms, error: {operationError}");

            public static void OperationStarted(ILogger logger, string operationName)
            {
                _operationStarted(logger, operationName, null);
            }

            public static void OperationEnded(ILogger logger, string operationName, double operationDuration)
            {
                _operationEnded(logger, operationName, operationDuration, null);
            }

            public static void OperationFailed(ILogger logger, string operationName, double operationDuration, string operationError)
            {
                _operationFailed(logger, operationName, operationDuration, operationError, null);
            }
        }
    }
}
