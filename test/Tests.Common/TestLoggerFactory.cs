// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using Microsoft.Extensions.Logging;

namespace Yarp.ReverseProxy.Common.Tests
{
    public class TestLoggerFactory : ILoggerFactory
    {
        public TestLogger Logger { get; } = new TestLogger();

        public void AddProvider(ILoggerProvider provider)
        {

        }

        public ILogger CreateLogger(string categoryName)
        {
            return Logger;
        }

        public void Dispose()
        {

        }
    }
}
