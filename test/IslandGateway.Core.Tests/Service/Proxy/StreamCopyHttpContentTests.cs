// <copyright file="StreamCopyHttpContentTests.cs" company="Microsoft Corporation">
// Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Xunit;

namespace IslandGateway.Core.Service.Proxy.Tests
{
    public class StreamCopyHttpContentTests
    {
        [Fact]
        public async Task CopyToAsync_InvokesStreamCopier()
        {
            // Arrange
            const int SourceSize = (128 * 1024) - 3;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            var streamCopierMock = new Mock<IStreamCopier>();
            streamCopierMock
                .Setup(s => s.CopyAsync(source, destination, It.IsAny<CancellationToken>()))
                .Returns(() => source.CopyToAsync(destination));

            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, CancellationToken.None);

            // Act & Assert
            sut.ConsumptionTask.IsCompleted.Should().BeFalse();
            sut.Started.Should().BeFalse();
            await sut.CopyToAsync(destination);

            sut.Started.Should().BeTrue();
            sut.ConsumptionTask.IsCompleted.Should().BeTrue();
            destination.ToArray().Should().BeEquivalentTo(sourceBytes);
        }

        [Fact]
        public async Task CopyToAsync_AsyncSequencing()
        {
            // Arrange
            var source = new MemoryStream();
            var destination = new MemoryStream();
            var streamCopierMock = new Mock<IStreamCopier>();
            var tcs = new TaskCompletionSource<bool>();
            streamCopierMock
                .Setup(s => s.CopyAsync(source, destination, It.IsAny<CancellationToken>()))
                .Returns(async () =>
                {
                    await tcs.Task;
                });

            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, CancellationToken.None);

            // Act & Assert
            sut.ConsumptionTask.IsCompleted.Should().BeFalse();
            sut.Started.Should().BeFalse();
            var task = sut.CopyToAsync(destination);

            sut.Started.Should().BeTrue(); // This should happen synchronously
            sut.ConsumptionTask.IsCompleted.Should().BeFalse(); // This cannot happen until the tcs releases it

            tcs.TrySetResult(true);
            await task;
            sut.ConsumptionTask.IsCompleted.Should().BeTrue();
        }

        [Fact]
        public async Task ReadAsStreamAsync_Throws()
        {
            // Arrange
            var source = new MemoryStream();
            var destination = new MemoryStream();
            var sut = new StreamCopyHttpContent(source, new Mock<IStreamCopier>().Object, CancellationToken.None);

            // Act
            Func<Task> func = () => sut.ReadAsStreamAsync();

            // Assert
            await func.Should().ThrowExactlyAsync<NotImplementedException>();
        }

        [Fact]
        public void AllowDuplex_ReturnsTrue()
        {
            // Arrange
            var source = new MemoryStream();
            var streamCopierMock = new Mock<IStreamCopier>();
            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, CancellationToken.None);

            // Assert
            // This is an internal property that HttpClient and friends use internally and which must be true
            // to support duplex channels.This test helps detect regressions or changes in undocumented behavior
            // in .NET Core, and it passes as of .NET Core 3.1.
            var allowDuplexProperty = typeof(HttpContent).GetProperty("AllowDuplex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            allowDuplexProperty.Should().NotBeNull();
            var allowDuplex = (bool)allowDuplexProperty.GetValue(sut);
            allowDuplex.Should().BeTrue();
        }
    }
}
