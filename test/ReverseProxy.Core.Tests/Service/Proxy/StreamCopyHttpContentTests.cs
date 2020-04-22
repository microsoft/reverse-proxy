// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Xunit;

namespace Microsoft.ReverseProxy.Core.Service.Proxy.Tests
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

            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, autoFlushHttpClientOutgoingStream: false, CancellationToken.None);

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
        [InlineData(true, 2)]
        public async Task CopyToAsync_AutoFlushing(bool autoFlush, int expectedFlushes)
        {
            // Arrange
            const int SourceSize = (128 * 1024) - 3;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();
            var flushCountingDestination = new FlushCountingStream(destination);

            var streamCopierMock = new Mock<IStreamCopier>();
            streamCopierMock
                .Setup(s => s.CopyAsync(source, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns((Stream source_, Stream destination_, CancellationToken cancellation_) =>
                    source_.CopyToAsync(destination_));

            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, autoFlushHttpClientOutgoingStream: autoFlush, CancellationToken.None);

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

            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, autoFlushHttpClientOutgoingStream: false, CancellationToken.None);

            // Act & Assert
            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            var task = sut.CopyToAsync(destination);

            Assert.True(sut.Started); // This should happen synchronously
            Assert.False(sut.ConsumptionTask.IsCompleted); // This cannot happen until the tcs releases it

            tcs.TrySetResult(true);
            await task;
            Assert.True(sut.ConsumptionTask.IsCompleted);
        }

        [Fact]
        public Task ReadAsStreamAsync_Throws()
        {
            // Arrange
            var source = new MemoryStream();
            var destination = new MemoryStream();
            var sut = new StreamCopyHttpContent(source, new Mock<IStreamCopier>().Object, autoFlushHttpClientOutgoingStream: false, CancellationToken.None);

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
            var streamCopierMock = new Mock<IStreamCopier>();
            var sut = new StreamCopyHttpContent(source, streamCopierMock.Object, autoFlushHttpClientOutgoingStream: false, CancellationToken.None);

            // Assert
            // This is an internal property that HttpClient and friends use internally and which must be true
            // to support duplex channels.This test helps detect regressions or changes in undocumented behavior
            // in .NET Core, and it passes as of .NET Core 3.1.
            var allowDuplexProperty = typeof(HttpContent).GetProperty("AllowDuplex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(allowDuplexProperty);
            var allowDuplex = (bool)allowDuplexProperty.GetValue(sut);
            Assert.True(allowDuplex);
        }

        private class FlushCountingStream : Stream
        {
            private readonly Stream _stream;

            public FlushCountingStream(Stream stream)
            {
                _stream = stream;
            }

            public int NumFlushes { get; private set; }

            public override bool CanRead => _stream.CanRead;

            public override bool CanSeek => _stream.CanSeek;

            public override bool CanWrite => _stream.CanWrite;

            public override long Length => _stream.Length;

            public override long Position
            {
                get => _stream.Position;
                set => _stream.Position = value;
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                return _stream.WriteAsync(buffer, offset, count, cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                return _stream.WriteAsync(buffer, cancellationToken);
            }

            public override async Task FlushAsync(CancellationToken cancellationToken)
            {
                await _stream.FlushAsync(cancellationToken);
                NumFlushes++;
            }

            public override void Flush()
            {
                _stream.Flush();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                return _stream.Read(buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return _stream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                _stream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                _stream.Write(buffer, offset, count);
            }
        }
    }
}
