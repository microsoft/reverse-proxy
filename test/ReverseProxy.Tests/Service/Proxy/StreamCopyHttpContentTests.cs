// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Utilities;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
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
            var sourceReader = PipeReader.Create(source);
            var destination = new MemoryStream();
            var destinationWriter = PipeWriter.Create(destination);

            var sut = new StreamCopyHttpContent(sourceReader, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

            // Act & Assert
            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            await sut.CopyToAsync(destination);

            Assert.True(sut.Started);
            Assert.True(sut.ConsumptionTask.IsCompleted);
            Assert.Equal(sourceBytes, destination.ToArray());
        }

        [Theory]
        [InlineData(false, 1)] // we expect to always flush at least once to trigger sending request headers
        [InlineData(true, 34)]
        public async Task CopyToAsync_AutoFlushing(bool autoFlush, int expectedFlushes)
        {
            // Arrange
            const int SourceSize = (128 * 1024) - 3;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var sourceReader = PipeReader.Create(source);
            var destination = new MemoryStream();
            var flushCountingDestination = new FlushCountingStream(destination);

            var sut = new StreamCopyHttpContent(sourceReader, autoFlushHttpClientOutgoingStream: autoFlush, new Clock(), CancellationToken.None);

            // Act & Assert
            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            await sut.CopyToAsync(flushCountingDestination);

            Assert.True(sut.Started);
            Assert.True(sut.ConsumptionTask.IsCompleted);
            Assert.Equal(sourceBytes, destination.ToArray());
            Assert.Equal(expectedFlushes, flushCountingDestination.NumFlushes);
        }

        [Fact]
        public async Task CopyToAsync_AsyncSequencing()
        {
            // Arrange
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = new Mock<Stream>();
            source.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>())).Returns(() => new ValueTask<int>(tcs.Task));
            var sourceReader = PipeReader.Create(source.Object);
            var destination = new MemoryStream();

            var sut = new StreamCopyHttpContent(sourceReader, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

            // Act & Assert
            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            var task = sut.CopyToAsync(destination);

            Assert.True(sut.Started); // This should happen synchronously
            Assert.False(sut.ConsumptionTask.IsCompleted); // This cannot happen until the tcs releases it

            tcs.TrySetResult(0);
            await task;
            Assert.True(sut.ConsumptionTask.IsCompleted);
        }

        [Fact]
        public Task ReadAsStreamAsync_Throws()
        {
            // Arrange
            var source = new MemoryStream();
            var sourceReader = PipeReader.Create(source);
            var destination = new MemoryStream();
            var sut = new StreamCopyHttpContent(sourceReader, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

            // Act
            Func<Task> func = () => sut.ReadAsStreamAsync();

            // Assert
            return Assert.ThrowsAsync<NotImplementedException>(func);
        }

        [Fact]
        public void AllowDuplex_ReturnsTrue()
        {
            // Arrange
            var source = new MemoryStream();
            var sourceReader = PipeReader.Create(source);
            var sut = new StreamCopyHttpContent(sourceReader, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

            // Assert
            // This is an internal property that HttpClient and friends use internally and which must be true
            // to support duplex channels.This test helps detect regressions or changes in undocumented behavior
            // in .NET Core, and it passes as of .NET Core 3.1.
            var allowDuplexProperty = typeof(HttpContent).GetProperty("AllowDuplex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(allowDuplexProperty);
            var allowDuplex = (bool)allowDuplexProperty.GetValue(sut);
            Assert.True(allowDuplex);
        }

        private class FlushCountingStream : DelegatingStream
        {
            public FlushCountingStream(Stream stream)
                : base(stream)
            { }

            public int NumFlushes { get; private set; }

            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                await base.FlushAsync(cancellationToken);
                NumFlushes++;
            }
        }
    }
}
