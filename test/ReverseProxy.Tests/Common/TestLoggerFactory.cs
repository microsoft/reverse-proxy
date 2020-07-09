using Microsoft.Extensions.Logging;

namespace Microsoft.ReverseProxy.Common
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
