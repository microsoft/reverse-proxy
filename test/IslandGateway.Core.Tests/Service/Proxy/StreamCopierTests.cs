// <copyright file="StreamCopierTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

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
        private readonly TestMetricCreator metricCreator;
        private readonly GatewayMetrics metrics;

        public StreamCopierTests()
        {
            this.metricCreator = this.Provide<IMetricCreator, TestMetricCreator>();
            this.metrics = this.Create<GatewayMetrics>();
        }

        [Fact]
        public void Constructor_Works()
        {
            new StreamCopier(this.metrics, default(StreamCopyTelemetryContext));
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
            var sut = new StreamCopier(this.metrics, in proxyTelemetryContext);

            // Act
            await sut.CopyAsync(source, destination, CancellationToken.None);

            // Assert
            destination.ToArray().Should().BeEquivalentTo(sourceBytes);
            this.metricCreator.MetricsLogged.Should().BeEquivalentTo(
                "StreamCopyBytes=131069;direction=upstream;backendId=be1;routeId=rt1;endpointId=ep1;protocol=",
                "StreamCopyIops=2;direction=upstream;backendId=be1;routeId=rt1;endpointId=ep1;protocol=");
        }
    }
}
