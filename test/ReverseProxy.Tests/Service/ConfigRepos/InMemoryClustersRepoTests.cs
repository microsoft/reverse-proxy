// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Abstractions;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Tests
{
    public class InMemoryClustersRepoTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new InMemoryClustersRepo();
        }

        [Fact]
        public void GetClustersAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();

            // Act
            var task = repo.GetClustersAsync(CancellationToken.None);

            // Assert
            Assert.True(task.IsCompleted, "should complete synchronously");
        }

        [Fact]
        public void SetClustersAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();

            // Act
            var task = repo.SetClustersAsync(new Dictionary<string, Cluster>(), CancellationToken.None);

            // Assert
            Assert.True(task.IsCompleted, "should complete synchronously");
        }

        [Fact]
        public async Task GetClustersAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.GetClustersAsync(cts.Token);
            }
        }

        [Fact]
        public async Task SetClustersAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.SetClustersAsync(new Dictionary<string, Cluster>(), cts.Token);
            }
        }

        [Fact]
        public async Task GetClustersAsync_StartsNull()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();

            // Act
            var result = await repo.GetClustersAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetClustersAsync_DeepClones()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();
            var clusters = new Dictionary<string, Cluster>
            {
                {  "cluster1", new Cluster { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 10 } } }
            };

            // Act
            await repo.SetClustersAsync(clusters, CancellationToken.None);

            // Modify input, should not affect output
            clusters["cluster1"].CircuitBreakerOptions.MaxConcurrentRequests = -1;
            clusters.Add("cluster2", new Cluster { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 30 } });

            var result = await repo.GetClustersAsync(CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.NotSame(clusters, result);
            Assert.NotSame(clusters["cluster1"].CircuitBreakerOptions, result["cluster1"].CircuitBreakerOptions);
            Assert.Equal(10, result["cluster1"].CircuitBreakerOptions.MaxConcurrentRequests);
        }

        [Fact]
        public async Task GetClustersAsync_DeepClones()
        {
            // Arrange
            var repo = new InMemoryClustersRepo();
            var clusters = new Dictionary<string, Cluster>
            {
                {  "cluster1", new Cluster { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 10 } } }
            };

            // Act
            await repo.SetClustersAsync(clusters, CancellationToken.None);
            var result1 = await repo.GetClustersAsync(CancellationToken.None);

            // Modify first results, should not affect future results
            result1["cluster1"].CircuitBreakerOptions.MaxConcurrentRequests = -1;
            result1.Add("cluster2", new Cluster { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 30 } });

            var result2 = await repo.GetClustersAsync(CancellationToken.None);

            // Assert
            Assert.Single(result2);
            Assert.NotSame(result1, result2);
            Assert.NotSame(clusters, result2);
            Assert.Equal(10, result2["cluster1"].CircuitBreakerOptions.MaxConcurrentRequests);
        }
    }
}
