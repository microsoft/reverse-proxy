// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

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
            metrics.StreamCopyBytes(123, "upstream", "be1", "rt1", "d1", "prot");

            // Assert
            Assert.Contains("StreamCopyBytes=123;direction=upstream;backendId=be1;routeId=rt1;destinationId=d1;protocol=prot", _metricCreator.MetricsLogged);
        }

        [Fact]
        public void StreamCopyIops_Works()
        {
            // Arrange
            var metrics = Create<ProxyMetrics>();

            // Act
            metrics.StreamCopyIops(123, "upstream", "be1", "rt1", "d1", "prot");

            // Assert
            Assert.Contains("StreamCopyIops=123;direction=upstream;backendId=be1;routeId=rt1;destinationId=d1;protocol=prot", _metricCreator.MetricsLogged);
        }
    }
}
