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
using Yarp.ReverseProxy.Common.Tests;
using Yarp.ReverseProxy.Utilities;

namespace Yarp.ReverseProxy.Service.Proxy.Tests
{
    public class StreamCopyHttpContentTests
    {
        [Fact]
        public async Task CopyToAsync_InvokesStreamCopier()
        {
            const int SourceSize = (128 * 1024) - 3;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            var sut = new StreamCopyHttpContent(source, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

            Assert.False(sut.ConsumptionTask.IsCompleted);
            Assert.False(sut.Started);
            await sut.CopyToAsync(destination);

            Assert.True(sut.Started);
            Assert.True(sut.ConsumptionTask.IsCompleted);
            Assert.Equal(sourceBytes, destination.ToArray());
        }

        [Theory]
        [InlineData(false)] // we expect to always flush at least once to trigger sending request headers
        [InlineData(true)]
        public async Task CopyToAsync_AutoFlushing(bool autoFlush)
        {
            // Must be same as StreamCopier constant.
            const int DefaultBufferSize = 65536;
            const int SourceSize = (128 * 1024) - 3;

            var expectedFlushes = 0;
            if (autoFlush)
            {
                // How many buffers is needed to send the source rounded up.
                expectedFlushes = (SourceSize - 1) / DefaultBufferSize + 1;
            }
            // Explicit flush after headers are sent.
            expectedFlushes++;

            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();
            var flushCountingDestination = new FlushCountingStream(destination);

            var sut = new StreamCopyHttpContent(source, autoFlushHttpClientOutgoingStream: autoFlush, new Clock(), CancellationToken.None);

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
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            var source = new Mock<Stream>();
            source.Setup(s => s.ReadAsync(It.IsAny<Memory<byte>>(), It.IsAny<CancellationToken>())).Returns(() => new ValueTask<int>(tcs.Task));
            var destination = new MemoryStream();

            var sut = new StreamCopyHttpContent(source.Object, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

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
            var source = new MemoryStream();
            var destination = new MemoryStream();
            var sut = new StreamCopyHttpContent(source, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

            Func<Task> func = () => sut.ReadAsStreamAsync();

            return Assert.ThrowsAsync<NotImplementedException>(func);
        }

        [Fact]
        public void AllowDuplex_ReturnsTrue()
        {
            var source = new MemoryStream();
            var sut = new StreamCopyHttpContent(source, autoFlushHttpClientOutgoingStream: false, new Clock(), CancellationToken.None);

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
