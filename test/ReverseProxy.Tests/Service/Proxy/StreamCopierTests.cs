// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Telemetry;
using Xunit;

namespace Microsoft.ReverseProxy.Service.Proxy.Tests
{
    public class StreamCopierTests : TestAutoMockBase
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CopyAsync_Works(bool isRequest)
        {
            var events = TestEventListener.Collect();

            const int SourceSize = (128 * 1024) - 3;
            var sourceBytes = Enumerable.Range(0, SourceSize).Select(i => (byte)(i % 256)).ToArray();
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            await StreamCopier.CopyAsync(isRequest, source, destination, CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());

            AssertContentTransferred(events, isRequest, SourceSize);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SourceThrows_Reported(bool isRequest)
        {
            var events = TestEventListener.Collect();

            var source = new ThrowStream();
            var destination = new MemoryStream();

            var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, CancellationToken.None);
            Assert.Equal(StreamCopyResult.InputError, result);
            Assert.IsAssignableFrom<IOException>(error);

            Assert.DoesNotContain(events, e => e.EventName.StartsWith("ContentTransfer"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DestinationThrows_Reported(bool isRequest)
        {
            var events = TestEventListener.Collect();

            var source = new MemoryStream(new byte[10]);
            var destination = new ThrowStream();

            var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, CancellationToken.None);
            Assert.Equal(StreamCopyResult.OutputError, result);
            Assert.IsAssignableFrom<IOException>(error);

            Assert.DoesNotContain(events, e => e.EventName.StartsWith("ContentTransfer"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Cancelled_Reported(bool isRequest)
        {
            var events = TestEventListener.Collect();

            var source = new MemoryStream(new byte[10]);
            var destination = new MemoryStream();

            var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, new CancellationToken(canceled: true));
            Assert.Equal(StreamCopyResult.Canceled, result);
            Assert.IsAssignableFrom<OperationCanceledException>(error);

            Assert.DoesNotContain(events, e => e.EventName.StartsWith("ContentTransfer"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SlowSourceStream_TelemetryReportsCorrectTime(bool isRequest)
        {
            var events = TestEventListener.Collect();

            const int SourceSize = 1;
            var sourceBytes = new byte[SourceSize];
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            await StreamCopier.CopyAsync(isRequest, new SlowStream(source, TimeSpan.FromMilliseconds(250)), destination, CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());

            AssertContentTransferred(events, isRequest, SourceSize,
                minReadTime: TimeSpan.FromMilliseconds(100), maxReadTime: TimeSpan.FromMilliseconds(1000),
                minWriteTime: TimeSpan.Zero, maxWriteTime: TimeSpan.FromMilliseconds(250));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SlowDestinationStream_TelemetryReportsCorrectTime(bool isRequest)
        {
            var events = TestEventListener.Collect();

            const int SourceSize = 1;
            var sourceBytes = new byte[SourceSize];
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            await StreamCopier.CopyAsync(isRequest, source, new SlowStream(destination, TimeSpan.FromMilliseconds(250)), CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());

            AssertContentTransferred(events, isRequest, SourceSize,
                minReadTime: TimeSpan.Zero, maxReadTime: TimeSpan.FromMilliseconds(250),
                minWriteTime: TimeSpan.FromMilliseconds(100), maxWriteTime: TimeSpan.FromMilliseconds(1000));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LongContentTransfer_TelemetryReportsTransferringEvents(bool isRequest)
        {
            var events = TestEventListener.Collect();

            const int BytesPerRead = 2;

            const int SourceSize = 6 * BytesPerRead;
            var sourceBytes = new byte[SourceSize];
            var source = new SlowStream(new MemoryStream(sourceBytes), TimeSpan.FromMilliseconds(500))
            {
                MaxBytesPerRead = BytesPerRead
            };
            var destination = new MemoryStream();

            await StreamCopier.CopyAsync(isRequest, source, destination, CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());

            AssertContentTransferred(events, isRequest, SourceSize,
                minFirstReadTime: TimeSpan.FromMilliseconds(100), maxFirstReadTime: TimeSpan.FromMilliseconds(1000),
                minReadTime: TimeSpan.FromSeconds(1), maxReadTime: TimeSpan.FromSeconds(10),
                minWriteTime: TimeSpan.Zero, maxWriteTime: TimeSpan.FromMilliseconds(250));

            var transferringEvents = events.Where(e => e.EventName == "ContentTransferring").ToArray();
            Assert.InRange(transferringEvents.Length, 2, 3);

            for (var i = 0; i < transferringEvents.Length; i++)
            {
                var payload = transferringEvents[i].Payload;
                Assert.Equal(5, payload.Count);

                Assert.Equal(isRequest, (bool)payload[0]);

                var contentLength = (long)payload[1];
                var iops = (long)payload[2];

                Assert.True(contentLength % BytesPerRead == 0);
                Assert.Equal(iops, contentLength / BytesPerRead);
                Assert.InRange(iops, i + 1, SourceSize / BytesPerRead);

                var readTime = (long)payload[3];
                Assert.InRange(readTime, TimeSpan.FromSeconds(i + 0.1).Ticks, TimeSpan.FromSeconds(i + 2).Ticks);

                var writeTime = (long)payload[4];
                Assert.InRange(writeTime, 0, TimeSpan.FromMilliseconds(250).Ticks);
            }
        }

        private static void AssertContentTransferred(List<EventWrittenEventArgs> events, bool isRequest, long contentLength,
            TimeSpan? minFirstReadTime = null, TimeSpan? maxFirstReadTime = null,
            TimeSpan? minReadTime = null, TimeSpan? maxReadTime = null,
            TimeSpan? minWriteTime = null, TimeSpan? maxWriteTime = null)
        {
            var payload = Assert.Single(events, e => e.EventName == "ContentTransferred").Payload;
            Assert.Equal(6, payload.Count);

            Assert.Equal(isRequest, (bool)payload[0]);
            Assert.Equal(contentLength, (long)payload[1]);

            var iops = (long)payload[2];
            Assert.InRange(iops, 1, contentLength + 1);

            var minFirstRead = minFirstReadTime.HasValue ? minFirstReadTime.Value.Ticks : 0;
            var maxFirstRead = maxFirstReadTime.HasValue ? maxFirstReadTime.Value.Ticks : TimeSpan.TicksPerMinute;

            var minRead = minReadTime.HasValue ? minReadTime.Value.Ticks : 0;
            var maxRead = maxReadTime.HasValue ? maxReadTime.Value.Ticks : TimeSpan.TicksPerMinute;

            var minWrite = minWriteTime.HasValue ? minWriteTime.Value.Ticks : 0;
            var maxWrite = maxWriteTime.HasValue ? maxWriteTime.Value.Ticks : TimeSpan.TicksPerMinute;

            var readTime = (long)payload[3];
            Assert.InRange(readTime, minRead, maxRead);

            var writeTime = (long)payload[4];
            Assert.InRange(writeTime, minWrite, maxWrite);

            var firstReadTime = (long)payload[5];
            Assert.InRange(firstReadTime, minFirstRead, maxFirstRead);

            Assert.True(firstReadTime <= readTime);

            var stages = events.GetProxyStages();

            var startStage = isRequest ? ProxyStage.RequestContentTransferStart : ProxyStage.ResponseContentTransferStart;
            var startTime = Assert.Single(stages, s => s.Stage == startStage).TimeStamp;

            var stopStage = isRequest ? ProxyStage.RequestContentTransferStop : ProxyStage.ResponseContentTransferStop;
            var stopTime = Assert.Single(stages, s => s.Stage == stopStage).TimeStamp;

            Assert.True(startTime <= stopTime);
        }

        private class ThrowStream : Stream
        {
            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => true;

            public override long Length => throw new NotSupportedException();

            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

            public override void Flush()
            {
                throw new NotSupportedException();
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                throw new NotImplementedException();
            }

            public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                throw new IOException("Fake connection issue");
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                throw new NotSupportedException();
            }

            public override void SetLength(long value)
            {
                throw new NotSupportedException();
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                throw new IOException("Fake connection issue");
            }
        }

        private class SlowStream : DelegatingStream
        {
            private readonly TimeSpan _waitTime;

            public int MaxBytesPerRead { get; set; } = int.MaxValue;

            public SlowStream(Stream innerStream, TimeSpan waitTime)
                : base(innerStream)
            {
                _waitTime = waitTime;
            }

            public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(_waitTime);
                return await base.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, MaxBytesPerRead)), cancellationToken);
            }

            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                await Task.Delay(_waitTime);
                await base.WriteAsync(buffer, cancellationToken);
            }
        }
    }
}
