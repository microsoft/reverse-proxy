// <copyright file="XunitLogger.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Tests.Common
{
    public class XunitLogger<TCategoryName> : ILogger<TCategoryName>
    {
        private readonly ITestOutputHelper testOutputHelper;
        private readonly string categoryName;

        public XunitLogger(ITestOutputHelper testOutputHelper)
        {
            this.testOutputHelper = testOutputHelper;
            this.categoryName = typeof(TCategoryName).FullName;
        }

        public IDisposable BeginScope<TState>(TState state)
            => NoopDisposable.Instance;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            this.testOutputHelper.WriteLine($"{this.categoryName}[{logLevel}][{eventId}] {formatter(state, exception)}");
            if (exception != null)
            {
                this.testOutputHelper.WriteLine(exception.ToString());
            }
        }

        private class NoopDisposable : IDisposable
        {
            private NoopDisposable()
            {
            }
            public static NoopDisposable Instance { get; set; } = new NoopDisposable();
            public void Dispose()
            {
            }
        }
    }
}