// <copyright file="InMemoryBackendsRepoTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using IslandGateway.Core.Abstractions;
using Xunit;

namespace IslandGateway.Core.Service.Tests
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
            task.IsCompleted.Should().BeTrue("should complete synchronously");
        }

        [Fact]
        public void SetBackendsAsync_CompletesSynchronously()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();

            // Act
            var task = repo.SetBackendsAsync(new Backend[0], CancellationToken.None);

            // Assert
            task.IsCompleted.Should().BeTrue("should complete synchronously");
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
                await repo.SetBackendsAsync(new Backend[0], cts.Token);
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
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetBackendsAsync_DeepClones()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();
            var backends = new List<Backend>
            {
                new Backend { BackendId = "backend1" },
            };

            // Act
            await repo.SetBackendsAsync(backends, CancellationToken.None);

            // Modify input, should not affect output
            backends[0].BackendId = "modified";
            backends.Add(new Backend { BackendId = "backend2" });

            var result = await repo.GetBackendsAsync(CancellationToken.None);

            // Assert
            result.Should().HaveCount(1);
            result.Should().NotBeSameAs(backends);
            result[0].BackendId.Should().Be("backend1");
        }

        [Fact]
        public async Task GetBackendsAsync_DeepClones()
        {
            // Arrange
            var repo = new InMemoryBackendsRepo();
            var backends = new[]
            {
                new Backend { BackendId = "backend1" },
            };

            // Act
            await repo.SetBackendsAsync(backends, CancellationToken.None);
            var result1 = await repo.GetBackendsAsync(CancellationToken.None);

            // Modify first results, should not affect future results
            result1[0].BackendId = "modified";
            result1.Add(new Backend { BackendId = "backend2" });

            var result2 = await repo.GetBackendsAsync(CancellationToken.None);

            // Assert
            result2.Should().HaveCount(1);
            result2.Should().NotBeSameAs(result1);
            result2.Should().NotBeSameAs(backends);
            result2[0].BackendId.Should().Be("backend1");
        }
    }
}
