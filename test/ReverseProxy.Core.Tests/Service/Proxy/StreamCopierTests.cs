// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Microsoft.ReverseProxy.Core.Service.Metrics;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
{
    public class StreamCopierTests : TestAutoMockBase
    {
        private readonly TestMetricCreator _metricCreator;
        private readonly ProxyMetrics _metrics;

        public StreamCopierTests()
        {
            _metricCreator = Provide<IMetricCreator, TestMetricCreator>();
            _metrics = Create<ProxyMetrics>();
        }

        [Fact]
        public void Constructor_Works()
        {
            new StreamCopier(_metrics, default);
        }

        [Fact]
        public async Task CopyAsync_Works()
        {
            // Arrange
            const int SourceSize = (128 * 1024) - 3;
            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();
            var proxyTelemetryContext = new StreamCopyTelemetryContext(
                direction: "upstream",
                backendId: "be1",
                routeId: "rt1",
                destinationId: "d1");
            var sut = new StreamCopier(_metrics, in proxyTelemetryContext);

            // Act
            await sut.CopyAsync(source, destination, CancellationToken.None);

            // Assert
            Assert.Equal(sourceBytes, destination.ToArray());
            Assert.Equal("StreamCopyBytes=131069;direction=upstream;backendId=be1;routeId=rt1;destinationId=d1;protocol=", _metricCreator.MetricsLogged[0]);
            Assert.Equal("StreamCopyIops=2;direction=upstream;backendId=be1;routeId=rt1;destinationId=d1;protocol=", _metricCreator.MetricsLogged[1]);
        }
    }
}
