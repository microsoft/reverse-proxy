// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Core.Abstractions;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Tests
{
    public class InMemoryBackendsRepoTests
    {
        [Fact]
        public void Constructor_Works()
        {
            new InMemoryBackendsRepo();
        }

        [Fact]
        public void GetBackendsAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();

            // Act
            var task = repo.GetBackendsAsync(CancellationToken.None);

            // Assert
            Assert.True(task.IsCompleted, "should complete synchronously");
        }

        [Fact]
        public void SetBackendsAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();

            // Act
            var task = repo.SetBackendsAsync(new Dictionary<string, Backend>(), CancellationToken.None);

            // Assert
            Assert.True(task.IsCompleted, "should complete synchronously");
        }

        [Fact]
        public async Task GetBackendsAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.GetBackendsAsync(cts.Token);
            }
        }

        [Fact]
        public async Task SetBackendsAsync_IgnoresCancellation()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();
            using (var cts = new CancellationTokenSource())
            {
                cts.Cancel();

                // Act & Assert
                await repo.SetBackendsAsync(new Dictionary<string, Backend>(), cts.Token);
            }
        }

        [Fact]
        public async Task GetBackendsAsync_StartsNull()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();

            // Act
            var result = await repo.GetBackendsAsync(CancellationToken.None);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetBackendsAsync_DeepClones()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();
            var backends = new Dictionary<string, Backend>
            {
                {  "backend1", new Backend { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 10 } } }
            };

            // Act
            await repo.SetBackendsAsync(backends, CancellationToken.None);

            // Modify input, should not affect output
            backends["backend1"].CircuitBreakerOptions.MaxConcurrentRequests = -1;
            backends.Add("backend2", new Backend { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 30 } });

            var result = await repo.GetBackendsAsync(CancellationToken.None);

            // Assert
            Assert.Single(result);
            Assert.NotEqual(backends, result);
            Assert.NotEqual(backends["backend1"].CircuitBreakerOptions, result["backend1"].CircuitBreakerOptions);
            Assert.Equal(10, result["backend1"].CircuitBreakerOptions.MaxConcurrentRequests);
        }

        [Fact]
        public async Task GetBackendsAsync_DeepClones()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();
            var backends = new Dictionary<string, Backend>
            {
                {  "backend1", new Backend { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 10 } } }
            };

            // Act
            await repo.SetBackendsAsync(backends, CancellationToken.None);
            var result1 = await repo.GetBackendsAsync(CancellationToken.None);

            // Modify first results, should not affect future results
            result1["backend1"].CircuitBreakerOptions.MaxConcurrentRequests = -1;
            result1.Add("backend2", new Backend { CircuitBreakerOptions = new CircuitBreakerOptions() { MaxConcurrentRequests = 30 } });

            var result2 = await repo.GetBackendsAsync(CancellationToken.None);

            // Assert
            Assert.Single(result2);
            Assert.NotEqual(result1, result2);
            Assert.NotEqual(backends, result2);
            Assert.Equal(10, result2["backend1"].CircuitBreakerOptions.MaxConcurrentRequests);
        }
    }
}
