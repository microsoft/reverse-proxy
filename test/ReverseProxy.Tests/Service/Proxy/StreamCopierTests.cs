// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ReverseProxy.Common.Tests;
using Microsoft.ReverseProxy.Telemetry;
using Microsoft.ReverseProxy.Utilities;
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

            await StreamCopier.CopyAsync(isRequest, source, destination, new Clock(), CancellationToken.None);

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

            var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, new Clock(), CancellationToken.None);
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

            var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, new Clock(), CancellationToken.None);
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

            var (result, error) = await StreamCopier.CopyAsync(isRequest, source, destination, new Clock(), new CancellationToken(canceled: true));
            Assert.Equal(StreamCopyResult.Canceled, result);
            Assert.IsAssignableFrom<OperationCanceledException>(error);

            Assert.DoesNotContain(events, e => e.EventName.StartsWith("ContentTransfer"));
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SlowStreams_TelemetryReportsCorrectTime(bool isRequest)
        {
            var events = TestEventListener.Collect();

            const int SourceSize = 3;
            var sourceBytes = new byte[SourceSize];
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            var clock = new ManualClock();
            var sourceWaitTime = TimeSpan.FromMilliseconds(12345);
            var destinationWaitTime = TimeSpan.FromMilliseconds(42);

            await StreamCopier.CopyAsync(
                isRequest,
                new SlowStream(source, clock, sourceWaitTime),
                new SlowStream(destination, clock, destinationWaitTime),
                clock,
                CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());

            AssertContentTransferred(events, isRequest, SourceSize,
                iops: SourceSize + 1,
                firstReadTime: sourceWaitTime,
                readTime: (SourceSize + 1) * sourceWaitTime,
                writeTime: SourceSize * destinationWaitTime);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task LongContentTransfer_TelemetryReportsTransferringEvents(bool isRequest)
        {
            var events = TestEventListener.Collect();

            const int SourceSize = 123;
            var sourceBytes = new byte[SourceSize];
            var source = new MemoryStream(sourceBytes);
            var destination = new MemoryStream();

            var clock = new ManualClock();
            var sourceWaitTime = TimeSpan.FromMilliseconds(789); // Every second read triggers ContentTransferring
            var destinationWaitTime = TimeSpan.FromMilliseconds(42);

            const int BytesPerRead = 3;
            var contentReads = (int)Math.Ceiling((double)SourceSize / BytesPerRead);

            await StreamCopier.CopyAsync(
                isRequest,
                new SlowStream(source, clock, sourceWaitTime) { MaxBytesPerRead = BytesPerRead },
                new SlowStream(destination, clock, destinationWaitTime),
                clock,
                CancellationToken.None);

            Assert.Equal(sourceBytes, destination.ToArray());

            AssertContentTransferred(events, isRequest, SourceSize,
                iops: contentReads + 1,
                firstReadTime: sourceWaitTime,
                readTime: (contentReads + 1) * sourceWaitTime,
                writeTime: contentReads * destinationWaitTime);

            var transferringEvents = events.Where(e => e.EventName == "ContentTransferring").ToArray();
            Assert.Equal(contentReads / 2, transferringEvents.Length);

            for (var i = 0; i < transferringEvents.Length; i++)
            {
                var payload = transferringEvents[i].Payload;
                Assert.Equal(5, payload.Count);

                Assert.Equal(isRequest, (bool)payload[0]);

                var contentLength = (long)payload[1];

                var iops = (long)payload[2];
                Assert.Equal((i + 1) * 2, iops);

                if (contentLength % BytesPerRead == 0)
                {
                    Assert.Equal(iops * BytesPerRead, contentLength);
                }
                else
                {
                    Assert.Equal(transferringEvents.Length - 1, i);
                    Assert.Equal(SourceSize, contentLength);
                }

                var readTime = new TimeSpan((long)payload[3]);
                Assert.Equal(iops * sourceWaitTime, readTime, new ApproximateTimeSpanComparer());

                var writeTime = new TimeSpan((long)payload[4]);
                Assert.Equal(iops * destinationWaitTime, writeTime, new ApproximateTimeSpanComparer());
            }
        }

        private static void AssertContentTransferred(
            List<EventWrittenEventArgs> events,
            bool isRequest,
            long contentLength,
            long? iops = null,
            TimeSpan? firstReadTime = null,
            TimeSpan? readTime = null,
            TimeSpan? writeTime = null)
        {
            var payload = Assert.Single(events, e => e.EventName == "ContentTransferred").Payload;
            Assert.Equal(6, payload.Count);

            Assert.Equal(isRequest, (bool)payload[0]);
            Assert.Equal(contentLength, (long)payload[1]);

            var actualIops = (long)payload[2];
            if (iops.HasValue)
            {
                Assert.Equal(iops.Value, actualIops);
            }
            else
            {
                Assert.InRange(actualIops, 1, contentLength + 1);
            }

            if (readTime.HasValue)
            {
                Assert.Equal(readTime.Value, new TimeSpan((long)payload[3]), new ApproximateTimeSpanComparer());
            }

            if (writeTime.HasValue)
            {
                Assert.Equal(writeTime.Value, new TimeSpan((long)payload[4]), new ApproximateTimeSpanComparer());
            }

            if (firstReadTime.HasValue)
            {
                Assert.Equal(firstReadTime.Value, new TimeSpan((long)payload[5]), new ApproximateTimeSpanComparer());

                if (readTime.HasValue)
                {
                    Assert.True(firstReadTime.Value <= readTime.Value);
                }
            }

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
            private readonly ManualClock _clock;

            public int MaxBytesPerRead { get; set; } = 1;

            public SlowStream(Stream innerStream, ManualClock clock, TimeSpan waitTime)
                : base(innerStream)
            {
                _clock = clock;
                _waitTime = waitTime;
            }

            public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                _clock.Time += _waitTime;
                return base.ReadAsync(buffer.Slice(0, Math.Min(buffer.Length, MaxBytesPerRead)), cancellationToken);
            }

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                _clock.Time += _waitTime;
                return base.WriteAsync(buffer, cancellationToken);
            }
        }

        private class ApproximateTimeSpanComparer : IEqualityComparer<TimeSpan>
        {
            public TimeSpan Precision { get; set; } = TimeSpan.FromMilliseconds(0.1);

            public bool Equals(TimeSpan x, TimeSpan y) => x > y
                ? x - y <= Precision
                : y - x <= Precision;

            public int GetHashCode(TimeSpan obj) => 42;
        }
    }
}
