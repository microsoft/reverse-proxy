// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.Common.Tests
{

    public class TestLogger : ILogger
    {
        public List<(LogLevel logLevel, EventId eventId, string message, Exception exception)> Logs { get; private set; } = new List<(LogLevel, EventId, string, Exception)>();

        public IEnumerable<(LogLevel logLevel, EventId eventId, string message, Exception exception)> Errors
            => Logs.Where(log => log.logLevel == LogLevel.Error);

        public IDisposable BeginScope<TState>(TState state)
        {
            return new NullDisposable();
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            Logs.Add((logLevel, eventId, formatter(state, exception), exception));
        }

        private class NullDisposable : IDisposable
        {
            public void Dispose()
            {

            }
        }
    }
}
