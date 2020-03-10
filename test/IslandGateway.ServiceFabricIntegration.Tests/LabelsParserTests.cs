// <copyright file="LabelsParserTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using System.IO;

using FluentAssertions;
using IslandGateway.Core.Abstractions;
using Xunit;

namespace IslandGateway.ServiceFabricIntegration.Tests
{
    public class LabelsParserTests
    {
        [Fact]
        public void BuildBackend_CompleteLabels_Works()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "IslandGateway.Enable", "true" },
                { "IslandGateway.Backend.BackendId", "MyCoolBackendId" },
                { "IslandGateway.Backend.CircuitBreaker.MaxConcurrentRequests", "42" },
                { "IslandGateway.Backend.CircuitBreaker.MaxConcurrentRetries", "5" },
                { "IslandGateway.Backend.Quota.Average", "1.2" },
                { "IslandGateway.Backend.Quota.Burst", "2.3" },
                { "IslandGateway.Backend.Partitioning.Count", "5" },
                { "IslandGateway.Backend.Partitioning.KeyExtractor", "Header('x-ms-organization-id')" },
                { "IslandGateway.Backend.Partitioning.Algorithm", "SHA256" },
                { "IslandGateway.Backend.Healthcheck.Interval", "PT5S" },
                { "IslandGateway.Backend.Healthcheck.Timeout", "PT5S" },
                { "IslandGateway.Backend.Healthcheck.Port", "8787" },
                { "IslandGateway.Backend.Healthcheck.Path", "/api/health" },
                { "IslandGateway.Backend.Metadata.Foo", "Bar" },
            };

            // Act
            var backend = LabelsParser.BuildBackend(labels);

            // Assert
            Backend expectedBackend = new Backend
            {
                BackendId = "MyCoolBackendId",
                CircuitBreakerOptions = new CircuitBreakerOptions
                {
                    MaxConcurrentRequests = 42,
                    MaxConcurrentRetries = 5,
                },
                QuotaOptions = new QuotaOptions
                {
                    Average = 1.2,
                    Burst = 2.3,
                },
                PartitioningOptions = new BackendPartitioningOptions
                {
                    PartitionCount = 5,
                    PartitionKeyExtractor = "Header('x-ms-organization-id')",
                    PartitioningAlgorithm = "SHA256",
                },
                LoadBalancingOptions = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions
                {
                    Interval = TimeSpan.FromSeconds(5),
                    Timeout = TimeSpan.FromSeconds(5),
                    Port = 8787,
                    Path = "/api/health",
                },
                Metadata = new Dictionary<string, string>
                {
                    { "Foo", "Bar" },
                },
            };

            backend.Should().BeEquivalentTo(expectedBackend);
        }

        [Fact]
        public void BuildBackend_IncompleteLabels_UsesDefaultValues()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "IslandGateway.Backend.BackendId", "MyCoolBackendId" },
            };

            // Act
            var backend = LabelsParser.BuildBackend(labels);

            // Assert
            Backend expectedBackend = new Backend
            {
                BackendId = "MyCoolBackendId",
                CircuitBreakerOptions = new CircuitBreakerOptions
                {
                    MaxConcurrentRequests = LabelsParser.DefaultCircuitbreakerMaxConcurrentRequests,
                    MaxConcurrentRetries = LabelsParser.DefaultCircuitbreakerMaxConcurrentRetries,
                },
                QuotaOptions = new QuotaOptions
                {
                    Average = LabelsParser.DefaultQuotaAverage,
                    Burst = LabelsParser.DefaultQuotaBurst,
                },
                PartitioningOptions = new BackendPartitioningOptions
                {
                    PartitionCount = LabelsParser.DefaultPartitionCount,
                    PartitionKeyExtractor = LabelsParser.DefaultPartitionKeyExtractor,
                    PartitioningAlgorithm = LabelsParser.DefaultPartitioningAlgorithm,
                },
                LoadBalancingOptions = new LoadBalancingOptions(),
                HealthCheckOptions = new HealthCheckOptions
                {
                    Interval = TimeSpan.Zero,
                    Timeout = TimeSpan.Zero,
                    Port = 0,
                    Path = null,
                },
                Metadata = new Dictionary<string, string>(),
            };

            backend.Should().BeEquivalentTo(expectedBackend);
        }

        [Fact]
        public void BuildBackend_MissingBackendId_Throws()
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "IslandGateway.Backend.Quota.Burst", "2.3" },
                { "IslandGateway.Backend.Partitioning.Count", "5" },
                { "IslandGateway.Backend.Partitioning.KeyExtractor", "Header('x-ms-organization-id')" },
                { "IslandGateway.Backend.Partitioning.Algorithm", "SHA256" },
                { "IslandGateway.Backend.Healthcheck.Interval", "PT5S" },
            };

            // Act
            Func<Backend> func = () => LabelsParser.BuildBackend(labels);

            // Assert
            func.Should().Throw<ServiceFabricIntegrationException>();
        }

        [Theory]
        [InlineData("IslandGateway.Backend.CircuitBreaker.MaxConcurrentRequests", "this is no number boi")]
        [InlineData("IslandGateway.Backend.CircuitBreaker.MaxConcurrentRetries", "P=NP")]
        [InlineData("IslandGateway.Backend.Quota.Average", "average should be a double")]
        [InlineData("IslandGateway.Backend.Quota.Burst", "")]
        [InlineData("IslandGateway.Backend.Partitioning.Count", "$#%+@`~áéÉ")]
        [InlineData("IslandGateway.Backend.Healthcheck.Interval", "1S")]
        [InlineData("IslandGateway.Backend.Healthcheck.Timeout", "foobar")]
        [InlineData("IslandGateway.Backend.Healthcheck.Port", "should be an int")]
        public void BuildBackend_InvalidValues_Throws(string key, string invalidValue)
        {
            // Arrange
            var labels = new Dictionary<string, string>()
            {
                { "IslandGateway.Backend.BackendId", "MyCoolBackendId" },
                { key, invalidValue },
            };

            // Act
            Func<Backend> func = () => LabelsParser.BuildBackend(labels);

            // Assert
            func.Should().Throw<ServiceFabricIntegrationException>();
        }
    }
}
