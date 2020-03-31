// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using FluentAssertions;
using Microsoft.ReverseProxy.Common.Abstractions.Telemetry;
using Tests.Common;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Metrics.Tests
{
    public class ProxyMetricsTests : TestAutoMockBase
    {
        private readonly TestMetricCreator _metricCreator;

        public ProxyMetricsTests()
        {
            _metricCreator = Provide<IMetricCreator, TestMetricCreator>();
        }

        [Fact]
        public void Constructor_Works()
        {
            Create<ProxyMetrics>();
        }

        [Fact]
        public void StreamCopyBytes_Works()
        {
            // Arrange
            var metrics = Create<ProxyMetrics>();

            // Act
            metrics.StreamCopyBytes(123, "upstream", "be1", "rt1", "ep1", "prot");

            // Assert
            _metricCreator.MetricsLogged.Should().BeEquivalentTo("StreamCopyBytes=123;direction=upstream;backendId=be1;routeId=rt1;endpointId=ep1;protocol=prot");
        }

        [Fact]
        public void StreamCopyIops_Works()
        {
            // Arrange
            var metrics = Create<ProxyMetrics>();

            // Act
            metrics.StreamCopyIops(123, "upstream", "be1", "rt1", "ep1", "prot");

            // Assert
            _metricCreator.MetricsLogged.Should().BeEquivalentTo("StreamCopyIops=123;direction=upstream;backendId=be1;routeId=rt1;endpointId=ep1;protocol=prot");
        }
    }
}
