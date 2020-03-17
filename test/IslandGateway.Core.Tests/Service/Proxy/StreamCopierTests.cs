// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IslandGateway.Common.Abstractions.Telemetry;
using IslandGateway.Core.Service.Metrics;
using Tests.Common;
using Xunit;

namespace IslandGateway.Core.Service.Proxy.Tests
{
    public class StreamCopierTests : TestAutoMockBase
    {
        private readonly TestMetricCreator _metricCreator;
        private readonly GatewayMetrics _metrics;

        public StreamCopierTests()
        {
            _metricCreator = Provide<IMetricCreator, TestMetricCreator>();
            _metrics = Create<GatewayMetrics>();
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
                endpointId: "ep1");
            var sut = new StreamCopier(_metrics, in proxyTelemetryContext);

            // Act
            await sut.CopyAsync(source, destination, CancellationToken.None);

            // Assert
            destination.ToArray().Should().BeEquivalentTo(sourceBytes);
            _metricCreator.MetricsLogged.Should().BeEquivalentTo(
                "StreamCopyBytes=131069;direction=upstream;backendId=be1;routeId=rt1;endpointId=ep1;protocol=",
                "StreamCopyIops=2;direction=upstream;backendId=be1;routeId=rt1;endpointId=ep1;protocol=");
        }
    }
}
