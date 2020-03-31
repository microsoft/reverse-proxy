// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.ReverseProxy.Core.Abstractions;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Tests
{
    public class InMemoryEndpointsRepoTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new InMemoryEndpointsRepo();
        }

        [Fact]
        public void GetEndpointsAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();

            // Act
            var task = repo.GetEndpointsAsync("nonexistent", CancellationToken.None);

            // Assert
            task.IsCompleted.Should().BeTrue("should complete synchronously");
        }

        [Fact]
        public void SetEndpointsAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();

            // Act
            var task = repo.SetEndpointsAsync("backend1", null, CancellationToken.None);

            // Assert
            task.IsCompleted.Should().BeTrue("should complete synchronously");
        }

        [Fact]
        public void RemoveEndpointsAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();

            // Act
            var task = repo.RemoveEndpointsAsync("backend1", CancellationToken.None);

            // Assert
            task.IsCompleted.Should().BeTrue("should complete synchronously");
        }

        [Fact]
        public async Task GetEndpointsAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.GetEndpointsAsync("backend1", cts.Token);
            }
        }

        [Fact]
        public async Task SetEndpointsAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.SetEndpointsAsync("backend1", null, cts.Token);
            }
        }

        [Fact]
        public async Task RemoveEndpointsAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.RemoveEndpointsAsync("backend1", cts.Token);
            }
        }

        [Fact]
        public async Task GetEndpointsAsync_NonExistentBackend()
        {
            // Arrange
            var repo = new InMemoryEndpointsRepo();

            // Act
            var result = await repo.GetEndpointsAsync("nonexistent", CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task RemoveEndpointsAsync_NonExistentBackend()
        {
            // Arrange
            const string TestBackendId = "nonexistent";
            var repo = new InMemoryEndpointsRepo();

            // Act
            await repo.RemoveEndpointsAsync(TestBackendId, CancellationToken.None);
            var endpoints = await repo.GetEndpointsAsync(TestBackendId, CancellationToken.None);

            // Assert
            endpoints.Should().BeNull();
        }

        [Fact]
        public async Task RemoveEndpointsAsync_ExistingBackend_Removes()
        {
            // Arrange
            const string TestBackendId = "nonexistent";
            var repo = new InMemoryEndpointsRepo();

            // Act
            await repo.SetEndpointsAsync(TestBackendId, new[] { new BackendEndpoint() }, CancellationToken.None);
            var endpoints1 = await repo.GetEndpointsAsync(TestBackendId, CancellationToken.None);
            await repo.RemoveEndpointsAsync(TestBackendId, CancellationToken.None);
            var endpoints2 = await repo.GetEndpointsAsync(TestBackendId, CancellationToken.None);

            // Assert
            endpoints1.Should().HaveCount(1);
            endpoints2.Should().BeNull();
        }

        [Fact]
        public async Task SetEndpointsAsync_DeepClones()
        {
            // Arrange
            const string TestBackendId = "backend1";

            var repo = new InMemoryEndpointsRepo();
            var endpoints = new List<BackendEndpoint>
            {
                new BackendEndpoint { EndpointId = "ep1" },
            };

            // Act
            await repo.SetEndpointsAsync(TestBackendId, endpoints, CancellationToken.None);

            // Modify input, should not affect output
            endpoints[0].EndpointId = "modified";
            endpoints.Add(new BackendEndpoint { EndpointId = "ep2" });

            var result = await repo.GetEndpointsAsync(TestBackendId, CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            result.Should().NotBeSameAs(endpoints);
            result[0].EndpointId.Should().Be("ep1");
        }

        [Fact]
        public async Task GetEndpointsAsync_DeepClones()
        {
            // Arrange
            const string TestBackendId = "backend1";

            var repo = new InMemoryEndpointsRepo();
            var endpoints = new List<BackendEndpoint>
            {
                new BackendEndpoint { EndpointId = "ep1" },
            };

            // Act
            await repo.SetEndpointsAsync(TestBackendId, endpoints, CancellationToken.None);
            var result1 = await repo.GetEndpointsAsync(TestBackendId, CancellationToken.None);

            // Modify first results, should not affect future results
            result1[0].EndpointId = "modified";
            result1.Add(new BackendEndpoint { EndpointId = "ep2" });

            var result2 = await repo.GetEndpointsAsync(TestBackendId, CancellationToken.None);

            // Assert
            result2.Should().HaveCount(1);
            result2.Should().NotBeSameAs(result1);
            result2.Should().NotBeSameAs(endpoints);
            result2[0].EndpointId.Should().Be("ep1");
        }
    }
}
