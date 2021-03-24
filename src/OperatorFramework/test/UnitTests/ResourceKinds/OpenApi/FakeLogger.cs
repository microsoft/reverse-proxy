// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;
using System;

namespace Microsoft.Kubernetes.ResourceKinds.OpenApi
{
    public sealed class FakeLogger<TCategoryName> : ILogger<TCategoryName>, IDisposable
    {
        public IDisposable BeginScope<TState>(TState state) => this;

        public void Dispose() { }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter) { }
    }
}
