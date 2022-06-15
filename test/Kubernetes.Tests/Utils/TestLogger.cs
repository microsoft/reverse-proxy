// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Yarp.Kubernetes.Tests.Utils;

public class TestLogger<T> : ILogger<T>
{
    private readonly ITestOutputHelper _output;
    private readonly LogLevel _minLogLevel;

    public TestLogger(ITestOutputHelper output, LogLevel minLogLevel = LogLevel.Debug)
    {
        _output = output;
        _minLogLevel = minLogLevel;
    }

    public IDisposable BeginScope<TState>(TState state)
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None && logLevel >= _minLogLevel;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
    {
        _output.WriteLine(formatter.Invoke(state, exception));
    }
}
